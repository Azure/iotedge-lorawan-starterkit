// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Shared;
    using Xunit;
    using XunitRetryHelper;

    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    /// <summary>
    /// Tests Cloud to Device messages
    /// </summary>
    public sealed class C2DMessageTest : IntegrationTestBaseCi
    {
        /// <summary>
        /// Identifies how many times a cloud to device message can be processor without failing a test.
        /// </summary>
        private const int CloudToDeviceMessageReceiveCountThreshold = 2;

        private static readonly Random random = new Random();

        public C2DMessageTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        /// <summary>
        /// Ensures that a cloud to device message has not been seen more than expected.
        /// </summary>
        /// <param name="foundCount">number of times found.</param>
        private void EnsureNotSeenTooManyTimes(int foundCount)
        {
            Assert.True(foundCount <= CloudToDeviceMessageReceiveCountThreshold, $"Cloud to device message was processed {foundCount} times");
            if (foundCount > 1)
            {
                TestLogger.Log($"[WARN] Cloud to device message was processed {foundCount} times");
            }
        }

        // Ensures that C2D messages are received when working with confirmed messages
        // RxDelay set up to be 2 seconds
        // Uses Device9_OTAA
        [RetryFact]
        public async Task Test_OTAA_Confirmed_Receives_C2D_Message_With_RX_Delay_2()
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;
            var device = this.TestFixtureCi.Device9_OTAA;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            await this.TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // find the gateway that accepted the join
            var joinAccept = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf("JoinAccept", StringComparison.OrdinalIgnoreCase) != -1);
            Assert.NotNull(joinAccept);
            Assert.NotNull(joinAccept.MatchedEvent);

            var targetGw = joinAccept.MatchedEvent.SourceId;
            Assert.Equal(device.GatewayID, targetGw);

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x confirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/{messagesToSend}");

                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+CMSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            this.Log($"Expected C2D received log is: {expectedRxSerial}");

            var c2dLogMessage = $"{device.DeviceID}: done completing cloud to device message, id: {c2dMessage.MessageId}";
            this.Log($"Expected C2D network server log is: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/{messagesToSend}");
                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(TimeSpan.FromSeconds(5));

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                // Check that RXDelay was correctly used
                if (this.ArduinoDevice.SerialLogs.Where(x => x.StartsWith("+CMSG: RXWIN1", StringComparison.OrdinalIgnoreCase)).Count() > 0)
                {
                    await this.TestFixtureCi.CheckAnswerTimingAsync(device.RXDelay * Constants.CONVERT_TO_PKT_FWD_TIME, false, device.GatewayID);
                }
                else if (this.ArduinoDevice.SerialLogs.Where(x => x.StartsWith("+CMSG: RXWIN2", StringComparison.OrdinalIgnoreCase)).Count() > 0)
                {
                    await this.TestFixtureCi.CheckAnswerTimingAsync(device.RXDelay * Constants.CONVERT_TO_PKT_FWD_TIME, true, device.GatewayID);
                }
                else
                {
                    Assert.True(false, "We were not able to determine in which windows the acknowledgement was submitted");
                }

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    this.EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    this.EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device10_OTAA
        [RetryFact]
        public async Task Test_OTAA_Unconfirmed_Receives_C2D_Message()
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;
            var device = this.TestFixtureCi.Device10_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            await this.TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x confirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/{messagesToSend}");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            this.Log($"Expected C2D received log is: {expectedRxSerial}");

            var c2dLogMessage = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}";
            this.Log($"Expected C2D received network server log is: {c2dLogMessage}");

            // Sends 8x confirmed messages
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    this.EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    this.EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message_Single()
        {
            return this.Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message(nameof(this.TestFixtureCi.Device15_OTAA));
        }

        /* Commented multi gateway tests as they make C2D tests flaky for now
        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message_MultiGw()
        {
            return this.Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message(nameof(this.TestFixtureCi.Device15_OTAA_MultiGw));
        }
        */

        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device15_OTAA
        private async Task Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message(string devicePropertyName)
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;
            var device = this.TestFixtureCi.GetDeviceByPropertyName(devicePropertyName);
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            await this.TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await this.TestFixtureCi.WaitForTwinSyncAfterJoinAsync(this.ArduinoDevice.SerialLogs, device.DeviceID);
            }

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 2,
                MessageId = Guid.NewGuid().ToString(),
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+MSG: PORT: 2; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            this.Log($"Expected C2D start with: {expectedRxSerial}");

            var c2dLogMessage = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}";
            this.Log($"Expected C2D received network server log is: {c2dLogMessage}");

            // Sends 8x unconfirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    this.EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    this.EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message_Single()
        {
            return this.Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message(nameof(this.TestFixtureCi.Device14_OTAA));
        }

        /* Commented multi gateway tests as they make C2D tests flaky for now
        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message_MultiGw()
        {
            return this.Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message(nameof(this.TestFixtureCi.Device14_OTAA_MultiGw));
        }
        */

        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device10_OTAA
        private async Task Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message(string devicePropertyName)
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;
            var device = this.TestFixtureCi.GetDeviceByPropertyName(devicePropertyName);
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);
            await this.TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await this.TestFixtureCi.WaitForTwinSyncAfterJoinAsync(this.ArduinoDevice.SerialLogs, device.DeviceID);
            }

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString(CultureInfo.InvariantCulture);
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

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            var expectedUDPMessageV1 = $"{device.DevAddr}: ConfirmedDataDown";
            var expectedUDPMessageV2 = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}, id: {msgId}, fport: 1, confirmed: True";
            this.Log($"Expected C2D received log is: {expectedRxSerial}");
            this.Log($"Expected UDP log starting with: {expectedUDPMessageV1} or {expectedUDPMessageV2}");

            // Sends 8x unconfirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(expectedUDPMessageV1, StringComparison.OrdinalIgnoreCase) || messageBody.StartsWith(expectedUDPMessageV2, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions
                    {
                        Description = $"{expectedUDPMessageV1} or {expectedUDPMessageV2}",
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    this.EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    this.EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find {expectedUDPMessageV1} or {expectedUDPMessageV2} in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        /// <summary>
        /// Ensures that a device that has preferred window set to two receives C2D messages.
        /// </summary>
        [RetryFact]
        public async Task C2D_When_Device_Has_Preferred_Windows_2_Should_Receive_In_2nd_Window_With_Custom_DR()
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;
            var device = this.TestFixtureCi.Device21_ABP;
            this.LogTestStart(device);
            // Setup LoRa device properties
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            // Setup protocol properties
            await this.ArduinoDevice.SetupLora(this.TestFixture.Configuration.LoraRegion);
            await this.TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixture.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString(CultureInfo.InvariantCulture);
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

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var foundReceivePacketInRX2Count = 0;
            var expectedRxSerial1 = $"+MSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            var expectedRxSerial2 = $"+MSG: RXWIN2";
            var expectedUDPMessageV1 = $"{device.DevAddr}: ConfirmedDataDown";
            var expectedUDPMessageV2 = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}, id: {msgId}, fport: 1, confirmed: True";
            this.Log($"Expected C2D received log is: {expectedRxSerial1} and {expectedRxSerial2}");
            this.Log($"Expected UDP log starting with: {expectedUDPMessageV1} or {expectedUDPMessageV2}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                var searchResults = await this.TestFixture.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(expectedUDPMessageV1, StringComparison.OrdinalIgnoreCase) || messageBody.StartsWith(expectedUDPMessageV2, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions
                    {
                        Description = $"{expectedUDPMessageV1} or {expectedUDPMessageV2}",
                        MaxAttempts = 1,
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    this.EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                if (this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial1))
                {
                    foundReceivePacketCount++;
                    this.Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    this.EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                if (this.ArduinoDevice.SerialLogs.Any(x => x.StartsWith(expectedRxSerial2, StringComparison.OrdinalIgnoreCase)))
                {
                    foundReceivePacketInRX2Count++;
                    this.Log($"{device.DeviceID}: Found C2D message (rx2) in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketInRX2Count} times");
                    this.EnsureNotSeenTooManyTimes(foundReceivePacketInRX2Count);
                }

                if (foundReceivePacketCount > 0 && foundReceivePacketInRX2Count > 0 && foundC2DMessageCount > 0)
                {
                    this.Log($"{device.DeviceID}: Found all messages in log (after sending {i}/{messagesToSend})");
                    break;
                }

                this.TestFixture.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find {expectedUDPMessageV1} or {expectedUDPMessageV2} in logs");

            // checks if serial received the message
            if (foundReceivePacketCount == 0)
            {
                if (this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial1))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial1}'");

            // checks if serial received the message in RX2
            if (foundReceivePacketInRX2Count == 0)
            {
                if (this.ArduinoDevice.SerialLogs.Any(x => x.StartsWith(expectedRxSerial2, StringComparison.OrdinalIgnoreCase)))
                {
                    foundReceivePacketInRX2Count++;
                }
            }

            Assert.True(foundReceivePacketInRX2Count > 0, $"Could not find lora receiving message '{expectedRxSerial2}'");
        }
    }
}
