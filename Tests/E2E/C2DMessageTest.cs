// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
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

        private static readonly Random Random = new Random();

        public C2DMessageTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        /// <summary>
        /// Ensures that a cloud to device message has not been seen more than expected.
        /// </summary>
        /// <param name="foundCount">number of times found.</param>
        private static void EnsureNotSeenTooManyTimes(int foundCount)
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
            var device = TestFixtureCi.Device9_OTAA;
            LogTestStart(device);

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEui);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            await TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // find the gateway that accepted the join
            const string expectedLog = "JoinAccept";
            var joinAccept = await TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(expectedLog, StringComparison.OrdinalIgnoreCase) != -1, new SearchLogOptions(expectedLog));
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
                Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/{messagesToSend}");

                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + Random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = FramePorts.App1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+CMSG: PORT: 1; RX: \"{ToHexString(c2dMessageBody)}\"";
            Log($"Expected C2D received log is: {expectedRxSerial}");

            var c2dLogMessage = $"{device.DeviceID}: done processing 'Complete' on cloud to device message, id: '{c2dMessage.MessageId}'";
            Log($"Expected C2D network server log is: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/{messagesToSend}");
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(TimeSpan.FromSeconds(5));

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                // Check that RXDelay was correctly used
                await TestFixtureCi.CheckAnswerTimingAsync(device.RXDelay, device.GatewayID);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var searchResults = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions(c2dLogMessage)
                    {
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
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
            var device = TestFixtureCi.Device10_OTAA;
            LogTestStart(device);
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEui);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            await TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x confirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending confirmed '{msg}' {i}/{messagesToSend}");

                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + Random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = FramePorts.App1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{ToHexString(c2dMessageBody)}\"";
            Log($"Expected C2D received log is: {expectedRxSerial}");

            var c2dLogMessage = $"{device.DeviceID}: cloud to device message: {ToHexString(c2dMessageBody)}";
            Log($"Expected C2D received network server log is: {c2dLogMessage}");

            // Sends 8x confirmed messages
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var searchResults = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions(c2dLogMessage)
                    {
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message_Single()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device15_OTAA));
            LogTestStart(device);
            return Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message(device);
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message_MultiGw()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device15_OTAA_MultiGw));
            LogTestStart(device);
            return Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message(device);
        }

        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device15_OTAA
        private async Task Test_OTAA_Unconfirmed_Receives_Confirmed_FPort_2_Message(TestDeviceInfo device)
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEui);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            await TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await TestFixtureCi.WaitForTwinSyncAfterJoinAsync(ArduinoDevice.SerialLogs, device.DevEui);
            }

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");

                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + Random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = FramePorts.App2,
                MessageId = Guid.NewGuid().ToString(),
            };

            await TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+MSG: PORT: 2; RX: \"{ToHexString(c2dMessageBody)}\"";
            Log($"Expected C2D start with: {expectedRxSerial}");

            var c2dLogMessage = $"{device.DeviceID}: cloud to device message: {ToHexString(c2dMessageBody)}";
            Log($"Expected C2D received network server log is: {c2dLogMessage}");

            // Sends 8x unconfirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var searchResults = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions(c2dLogMessage)
                    {
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial}'");
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message_Single()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device14_OTAA));
            LogTestStart(device);
            return Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message(device);
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message_MultiGw()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device14_OTAA_MultiGw));
            LogTestStart(device);
            return Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message(device);
        }


        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device10_OTAA
        private async Task Test_OTAA_Unconfirmed_Receives_Confirmed_C2D_Message(TestDeviceInfo device)
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEui);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);
            await TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await TestFixtureCi.WaitForTwinSyncAfterJoinAsync(ArduinoDevice.SerialLogs, device.DevEui);
            }

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");

                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                TestFixtureCi.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + Random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var msgId = Guid.NewGuid().ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = FramePorts.App1,
                MessageId = msgId,
                Confirmed = true,
            };

            await TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{ToHexString(c2dMessageBody)}\"";
            var expectedTcpMessageV1 = $"{device.DevAddr}: ConfirmedDataDown";
            var expectedTcpMessageV2 = $"{device.DeviceID}: cloud to device message: {ToHexString(c2dMessageBody)}, id: {msgId}, fport: 1, confirmed: True";
            Log($"Expected C2D received log is: {expectedRxSerial}");
            Log($"Expected TCP log starting with: {expectedTcpMessageV1} or {expectedTcpMessageV2}");

            // Sends 8x unconfirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // check if c2d message was found
                var searchResults = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(expectedTcpMessageV1, StringComparison.OrdinalIgnoreCase) || messageBody.StartsWith(expectedTcpMessageV2, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions($"{expectedTcpMessageV1} or {expectedTcpMessageV2}")
                    {
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                var localFoundCloudToDeviceInSerial = ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    foundReceivePacketCount++;
                    Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find {expectedTcpMessageV1} or {expectedTcpMessageV2} in logs");

            // checks if log arrived
            if (foundReceivePacketCount == 0)
            {
                if (ArduinoDevice.SerialLogs.Contains(expectedRxSerial))
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
            var device = TestFixtureCi.Device21_ABP;
            LogTestStart(device);
            // Setup LoRa device properties
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEui);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            // Setup protocol properties
            await ArduinoDevice.SetupLora(TestFixture.Configuration);
            await TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");

                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                TestFixture.ClearLogs();
            }

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + Random.Next(90)).ToString(CultureInfo.InvariantCulture);
            var msgId = Guid.NewGuid().ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = FramePorts.App1,
                MessageId = msgId,
                Confirmed = true,
            };

            await TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);

            Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessageCount = 0;
            var foundReceivePacketCount = 0;
            var foundReceivePacketInRX2Count = 0;
            var expectedRxSerial1 = $"+MSG: PORT: 1; RX: \"{ToHexString(c2dMessageBody)}\"";
            var expectedRxSerial2 = $"+MSG: RXWIN2";
            var expectedTcpMessageV1 = $"{device.DevAddr}: ConfirmedDataDown";
            var expectedTcpMessageV2 = $"{device.DeviceID}: cloud to device message: {ToHexString(c2dMessageBody)}, id: {msgId}, fport: 1, confirmed: True";
            Log($"Expected C2D received log is: {expectedRxSerial1} and {expectedRxSerial2}");
            Log($"Expected TCP log starting with: {expectedTcpMessageV1} or {expectedTcpMessageV2}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // check if c2d message was found
                var searchResults = await TestFixture.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(expectedTcpMessageV1, StringComparison.OrdinalIgnoreCase) || messageBody.StartsWith(expectedTcpMessageV2, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions($"{expectedTcpMessageV1} or {expectedTcpMessageV2}")
                    {
                        MaxAttempts = 1,
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    foundC2DMessageCount++;
                    Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/{messagesToSend}) {foundC2DMessageCount} times");
                    EnsureNotSeenTooManyTimes(foundC2DMessageCount);
                }

                if (ArduinoDevice.SerialLogs.Contains(expectedRxSerial1))
                {
                    foundReceivePacketCount++;
                    Log($"{device.DeviceID}: Found C2D message in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketCount} times");
                    EnsureNotSeenTooManyTimes(foundReceivePacketCount);
                }

                if (ArduinoDevice.SerialLogs.Any(x => x.StartsWith(expectedRxSerial2, StringComparison.OrdinalIgnoreCase)))
                {
                    foundReceivePacketInRX2Count++;
                    Log($"{device.DeviceID}: Found C2D message (rx2) in serial logs (after sending {i}/{messagesToSend}) {foundReceivePacketInRX2Count} times");
                    EnsureNotSeenTooManyTimes(foundReceivePacketInRX2Count);
                }

                if (foundReceivePacketCount > 0 && foundReceivePacketInRX2Count > 0 && foundC2DMessageCount > 0)
                {
                    Log($"{device.DeviceID}: Found all messages in log (after sending {i}/{messagesToSend})");
                    break;
                }

                TestFixture.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessageCount > 0, $"Did not find {expectedTcpMessageV1} or {expectedTcpMessageV2} in logs");

            // checks if serial received the message
            if (foundReceivePacketCount == 0)
            {
                if (ArduinoDevice.SerialLogs.Contains(expectedRxSerial1))
                {
                    foundReceivePacketCount++;
                }
            }

            Assert.True(foundReceivePacketCount > 0, $"Could not find lora receiving message '{expectedRxSerial1}'");

            // checks if serial received the message in RX2
            if (foundReceivePacketInRX2Count == 0)
            {
                if (ArduinoDevice.SerialLogs.Any(x => x.StartsWith(expectedRxSerial2, StringComparison.OrdinalIgnoreCase)))
                {
                    foundReceivePacketInRX2Count++;
                }
            }

            Assert.True(foundReceivePacketInRX2Count > 0, $"Could not find lora receiving message '{expectedRxSerial2}'");
        }
    }
}
