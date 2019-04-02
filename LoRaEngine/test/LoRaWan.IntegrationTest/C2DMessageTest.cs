// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaTools.Utils;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices;
    using Xunit;

    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    /// <summary>
    /// Tests Cloud to Device messages
    /// </summary>
    public sealed class C2DMessageTest : IntegrationTestBaseCi
    {
        private const string FportPropertyName = "fport";
        private const string ConfirmedPropertyName = "Confirmed";
        private static Random random = new Random();

        public C2DMessageTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        private string ToHexString(string str)
        {
            var sb = new StringBuilder();

            var bytes = Encoding.UTF8.GetBytes(str);
            foreach (var t in bytes)
            {
                sb.Append(t.ToString("X2"));
            }

            return sb.ToString(); // returns: "48656C6C6F20776F726C64" for "Hello world"
        }

        // Ensures that C2D messages are received when working with confirmed messages
        // RxDelay set up to be 2 seconds
        // Uses Device9_OTAA
        [Fact]
        public async Task Test_OTAA_Confirmed_Receives_C2D_Message_With_RX_Delay_2()
        {
            var device = this.TestFixtureCi.Device9_OTAA;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // find the gateway that accepted the join
            var joinAccept = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf("JoinAccept") != -1);
            Assert.NotNull(joinAccept);
            Assert.NotNull(joinAccept.MatchedEvent);

            var targetGw = joinAccept.MatchedEvent.SourceId;
            Assert.Equal(device.GatewayID, targetGw);

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x confirmed messages
            for (var i = 1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/10");

                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+CMSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            this.Log($"Expected C2D received log is: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = 3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/10");
                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(TimeSpan.FromSeconds(5));

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                // Check that RXDelay was correctly used
                if (this.ArduinoDevice.SerialLogs.Where(x => x.StartsWith("+CMSG: RXWIN1")).Count() > 0)
                {
                    await this.TestFixtureCi.CheckAnswerTimingAsync(device.RXDelay * Constants.CONVERT_TO_PKT_FWD_TIME, false, device.GatewayID);
                }
                else if (this.ArduinoDevice.SerialLogs.Where(x => x.StartsWith("+CMSG: RXWIN2")).Count() > 0)
                {
                    await this.TestFixtureCi.CheckAnswerTimingAsync(device.RXDelay * Constants.CONVERT_TO_PKT_FWD_TIME, true, device.GatewayID);
                }
                else
                {
                    Assert.True(false, "We were not able to determine in which windows the acknowledgement was submitted");
                }

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var c2dLogMessage = $"{device.DeviceID}: done completing cloud to device message, id: {c2dMessage.MessageId}";
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
            {
                foundReceivePacket = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
            }

            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device10_OTAA
        [Fact]
        public async Task Test_OTAA_Unconfirmed_Receives_C2D_Message()
        {
            var device = this.TestFixtureCi.Device10_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x confirmed messages
            for (var i = 1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            this.Log($"Expected C2D received log is: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = 3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var c2dLogMessage = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}";
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
            {
                foundReceivePacket = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
            }

            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device15_OTAA
        [Fact]
        public async Task Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message()
        {
            var device = this.TestFixtureCi.Device15_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 2,
                MessageId = Guid.NewGuid().ToString(),
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+MSG: PORT: 2; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            this.Log($"Expected C2D start with: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = 3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var c2dLogMessage = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}";
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
            {
                foundReceivePacket = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
            }

            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device10_OTAA
        [Fact]
        public async Task Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message()
        {
            var device = this.TestFixtureCi.Device14_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            var msgId = Guid.NewGuid().ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 1,
                MessageId = msgId,
                Confirmed = true,
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            var expectedUDPMessageV1 = $"{device.DevAddr}: ConfirmedDataDown";
            var expectedUDPMessageV2 = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}, id: {msgId}, fport: 1, confirmed: True";
            this.Log($"Expected C2D received log is: {expectedRxSerial}");
            this.Log($"Expected UDP log starting with: {expectedUDPMessageV1} or {expectedUDPMessageV2}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = 3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(expectedUDPMessageV1) || messageBody.StartsWith(expectedUDPMessageV2);
                    },
                    new SearchLogOptions
                    {
                        Description = $"{expectedUDPMessageV1} or {expectedUDPMessageV2}",
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find {expectedUDPMessageV1} or {expectedUDPMessageV2} in logs");

            // checks if log arrived
            if (!foundReceivePacket)
            {
                foundReceivePacket = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
            }

            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        /// <summary>
        /// Ensures that a device that has preferred window set to two receives C2D messages
        /// </summary>
        [Fact]
        public async Task C2D_When_Device_Has_Preferred_Windows_2_Should_Receive_In_2nd_Window_With_Custom_DR()
        {
            var device = this.TestFixtureCi.Device21_ABP;
            this.LogTestStart(device);
            // Setup LoRa device properties
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            // Setup protocol properties
            await this.ArduinoDevice.SetupLora(this.TestFixture.Configuration.LoraRegion);

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixture.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            var msgId = Guid.NewGuid().ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 1,
                MessageId = msgId,
                Confirmed = true,
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);

            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var foundReceivePacketInRX2 = false;
            var expectedRxSerial1 = $"+MSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            var expectedRxSerial2 = $"+MSG: RXWIN2";
            var expectedUDPMessageV1 = $"{device.DevAddr}: ConfirmedDataDown";
            var expectedUDPMessageV2 = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}, id: {msgId}, fport: 1, confirmed: True";
            this.Log($"Expected C2D received log is: {expectedRxSerial1} and {expectedRxSerial2}");
            this.Log($"Expected UDP log starting with: {expectedUDPMessageV1} or {expectedUDPMessageV2}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = 3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                if (!foundC2DMessage)
                {
                    var searchResults = await this.TestFixture.SearchNetworkServerModuleAsync(
                        (messageBody) =>
                        {
                            return messageBody.StartsWith(expectedUDPMessageV1) || messageBody.StartsWith(expectedUDPMessageV2);
                        },
                        new SearchLogOptions
                        {
                            Description = $"{expectedUDPMessageV1} or {expectedUDPMessageV2}",
                            MaxAttempts = 1,
                        });

                    // We should only receive the message once
                    if (searchResults.Found)
                    {
                        this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/10)");
                        foundC2DMessage = true;
                    }
                }

                if (!foundReceivePacket)
                {
                    if (this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial1))
                    {
                        this.Log($"{device.DeviceID}: Found serial log message {expectedRxSerial1} in log (after sending {i}/10)");
                        foundReceivePacket = true;
                    }
                }

                if (!foundReceivePacketInRX2)
                {
                    if (this.ArduinoDevice.SerialLogs.Any(x => x.StartsWith(expectedRxSerial2)))
                    {
                        this.Log($"{device.DeviceID}: Found serial log message {expectedRxSerial2} in log (after sending {i}/10)");
                        foundReceivePacketInRX2 = true;
                    }
                }

                if (foundReceivePacket && foundReceivePacketInRX2 && foundC2DMessage)
                {
                    this.Log($"{device.DeviceID}: Found all messages in log (after sending {i}/10)");
                    break;
                }

                this.TestFixture.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find {expectedUDPMessageV1} or {expectedUDPMessageV2} in logs");

            // checks if serial received the message
            if (!foundReceivePacket)
            {
                foundReceivePacket = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial1);
            }

            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial1}'");

            // checks if serial received the message in RX2
            if (!foundReceivePacketInRX2)
            {
                foundReceivePacketInRX2 = this.ArduinoDevice.SerialLogs.Any(x => x.StartsWith(expectedRxSerial2));
            }

            Assert.True(foundReceivePacketInRX2, $"Could not find lora receiving message '{expectedRxSerial2}'");
        }
    }
}