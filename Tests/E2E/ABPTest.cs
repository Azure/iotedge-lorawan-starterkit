// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Xunit;
    using XunitRetryHelper;

    // Tests ABP requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class ABPTest : IntegrationTestBaseCi
    {
        public ABPTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [RetryFact]
        public Task Test_ABP_Confirmed_And_Unconfirmed_Message_With_ADR_Single()
        {
            return Test_ABP_Confirmed_And_Unconfirmed_Message_With_ADR(nameof(TestFixtureCi.Device5_ABP));
        }

        [RetryFact]
        public Task Test_ABP_Confirmed_And_Unconfirmed_Message_With_ADR_MultiGw()
        {
            return Test_ABP_Confirmed_And_Unconfirmed_Message_With_ADR(nameof(TestFixtureCi.Device5_ABP_MultiGw));
        }

        // Verifies that ABP confirmed and unconfirmed messages are working
        // Uses Device5_ABP
        private async Task Test_ABP_Confirmed_And_Unconfirmed_Message_With_ADR(string devicePropertyName)
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(devicePropertyName);

            if (device.IsMultiGw)
            {
                Assert.True(await LoRaAPIHelper.ResetADRCache(device.DeviceID));
            }

            await ArduinoDevice.setDeviceDefaultAsync();
            const int MESSAGES_COUNT = 10;
            LogTestStart(device);
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion, LoRaArduinoSerial._data_rate_t.DR3, 4, true);
            // for a reason I need to set DR twice otherwise it reverts to DR 0
            // await ArduinoDevice.setDataRateAsync(LoRaArduinoSerial._data_rate_t.DR3, LoRaArduinoSerial._physical_type_t.EU868);
            // Sends 5x unconfirmed messages
            for (var i = 0; i < MESSAGES_COUNT / 2; ++i)
            {
                var msg = GeneratePayloadMessage();
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i + 1}/{MESSAGES_COUNT}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                TestFixtureCi.ClearLogs();
            }

            // Sends 5x confirmed messages
            for (var i = 0; i < MESSAGES_COUNT / 2; ++i)
            {
                var msg = GeneratePayloadMessage();
                Log($"{device.DeviceID}: Sending confirmed '{msg}' {i + 1}/{MESSAGES_COUNT / 2}");
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(2 * Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                if (device.IsMultiGw)
                {
                    var searchTokenSending = $"{device.DeviceID}: sending a downstream message";
                    var sending = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenSending, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(sending.MatchedEvent);

                    var searchTokenAlreadySent = $"{device.DeviceID}: another gateway has already sent ack or downlink msg";
                    var ignored = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenAlreadySent, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(ignored.MatchedEvent);

                    Assert.NotEqual(sending.MatchedEvent.SourceId, ignored.MatchedEvent.SourceId);
                }

                TestFixtureCi.ClearLogs();
            }

            // Sends 10x unconfirmed messages
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                var msg = GeneratePayloadMessage();
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i + 1}/{MESSAGES_COUNT / 2}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                TestFixtureCi.ClearLogs();
            }

            // Starting ADR test protocol
            Log($"{device.DeviceID}: Starting ADR protocol");

            for (var i = 0; i < 56; ++i)
            {
                var message = GeneratePayloadMessage();
                await ArduinoDevice.transferPacketAsync(message, 10);
                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);
            }

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: ADR ack request received");

            var searchTokenADRRateAdaptation = $"{device.DeviceID}: performing a rate adaptation: DR";
            var received = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenADRRateAdaptation, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(received.MatchedEvent);

            if (device.IsMultiGw)
            {
                var searchTokenADRAlreadySent = $"{device.DeviceID}: another gateway has already sent ack or downlink msg";
                var ignored = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenADRAlreadySent, StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(ignored.MatchedEvent);
                Assert.NotEqual(received.MatchedEvent.SourceId, ignored.MatchedEvent.SourceId);
            }

            // Check the messages are now sent on DR5
            for (var i = 0; i < 2; ++i)
            {
                var message = GeneratePayloadMessage();
                await ArduinoDevice.transferPacketAsync(message, 10);
                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: LinkADRCmd mac command detected in upstream payload: Type: LinkADRCmd Answer, power: changed, data rate: changed,", $"{device.DevAddr}: LinkADRCmd mac command detected in upstream payload: Type: LinkADRCmd Answer, power: not changed, data rate: changed,");
            }
        }

        [RetryFact]
        public Task Test_ABP_Wrong_DevAddr_Is_Ignored_05060708()
        {
            return Test_ABP_Wrong_DevAddr_Is_Ignored("05060708");
        }

        [RetryFact]
        public Task Test_ABP_Wrong_DevAddr_Is_Ignored_02060708()
        {
            return Test_ABP_Wrong_DevAddr_Is_Ignored("02060708");
        }

        // Verifies that ABP using wrong devAddr is ignored when sending messages
        // Uses Device6_ABP
        private async Task Test_ABP_Wrong_DevAddr_Is_Ignored(string devAddrToUse)
        {
            var device = TestFixtureCi.Device6_ABP;
            LogTestStart(device);

            Assert.NotEqual(devAddrToUse, device.DevAddr);
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(devAddrToUse, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);

            await ArduinoDevice.transferPacketAsync(GeneratePayloadMessage(), 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);
            if (devAddrToUse.StartsWith("02", StringComparison.Ordinal))
            {
                // 02060708: device is not our device, ignore message
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                    $"{devAddrToUse}: device is not our device, ignore message",
                    $"{devAddrToUse}: device is not from our network, ignoring message");
            }
            else
            {
                // 05060708: device is using another network id, ignoring this message
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                 $"{devAddrToUse}: device is using another network id, ignoring this message");
            }

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            TestFixtureCi.ClearLogs();

            // Try with confirmed message
            await ArduinoDevice.transferPacketWithConfirmedAsync(GeneratePayloadMessage(), 10);

            // wait for serial logs to be ready
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacketWithConfirmed: Expectation from serial
            // +CMSG: ACK Received -- should not be there!
            Assert.DoesNotContain("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

            if (devAddrToUse.StartsWith("02", StringComparison.Ordinal))
            {
                // 02060708: device is not our device, ignore message
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                    $"{devAddrToUse}: device is not our device, ignore message",
                    $"{devAddrToUse}: device is not from our network, ignoring message");
            }
            else
            {
                // 05060708: device is using another network id, ignoring this message
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                 $"{devAddrToUse}: device is using another network id, ignoring this message");
            }
        }

        // Tests using a incorrect Network Session key, resulting device not ours
        // AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
        // NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
        // DevAddr="0028B1B2"
        // Uses Device7_ABP
        [RetryFact]
        public async Task Test_ABP_Mismatch_NwkSKey_And_AppSKey_Fails_Mic_Validation()
        {
            var device = TestFixtureCi.Device7_ABP;
            LogTestStart(device);

            var appSKeyToUse = "000102030405060708090A0B0C0D0E0F";
            var nwkSKeyToUse = "01020304050607080910111213141516";
            Assert.NotEqual(appSKeyToUse, device.AppSKey);
            Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(nwkSKeyToUse, appSKeyToUse, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);

            await ArduinoDevice.transferPacketAsync(GeneratePayloadMessage(), 10);

            // wait for serial logs to be ready
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            // await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.lora.SerialLogs);

            // 0000000000000005: with devAddr 0028B1B0 check MIC failed. Device will be ignored from now on
            await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            TestFixtureCi.ClearLogs();

            // Try with confirmed message
            await ArduinoDevice.transferPacketWithConfirmedAsync(GeneratePayloadMessage(), 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // 0000000000000005: with devAddr 0028B1B0 check MIC failed
            await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            // wait until arduino stops trying to send confirmed msg
            await ArduinoDevice.WaitForIdleAsync();
        }

        // Tests using a invalid Network Session key, resulting in mic failed
        // Uses Device8_ABP
        [RetryFact]
        public async Task Test_ABP_Invalid_NwkSKey_Fails_With_Mic_Error()
        {
            var device = TestFixtureCi.Device8_ABP;
            LogTestStart(device);

            var nwkSKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(nwkSKeyToUse, device.AppSKey, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);

            await ArduinoDevice.transferPacketAsync(GeneratePayloadMessage(), 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

            // 0000000000000008: with devAddr 0028B1B3 check MIC failed. Device will be ignored from now on
            await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            TestFixtureCi.ClearLogs();

            // Try with confirmed message
            await ArduinoDevice.transferPacketWithConfirmedAsync(GeneratePayloadMessage(), 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // 0000000000000008: with devAddr 0028B1B3 check MIC failed.
            await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            // Before starting new test, wait until Lora drivers stops sending/receiving data
            await ArduinoDevice.WaitForIdleAsync();
        }

        // Verifies that ABP confirmed and unconfirmed messages are working
        // Uses Device16_ABP and Device17_ABP
        [RetryFact]
        public async Task Test_ABP_Device_With_Same_DevAddr()
        {
            const int MESSAGES_COUNT = 2;
            LogTestStart(new TestDeviceInfo[] { TestFixtureCi.Device16_ABP, TestFixtureCi.Device17_ABP });

            await SendABPMessages(MESSAGES_COUNT, TestFixtureCi.Device16_ABP);
            await SendABPMessages(MESSAGES_COUNT, TestFixtureCi.Device17_ABP);
        }

        private async Task SendABPMessages(int messages_count, TestDeviceInfo device)
        {
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);

            // Sends 10x unconfirmed messages
            for (var i = 0; i < messages_count; ++i)
            {
                var msg = GeneratePayloadMessage();
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i + 1}/{messages_count}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                TestFixtureCi.ClearLogs();
            }

            // Sends 10x confirmed messages
            for (var i = 0; i < messages_count; ++i)
            {
                var msg = GeneratePayloadMessage();
                Log($"{device.DeviceID}: Sending confirmed '{msg}' {i + 1}/{messages_count}");
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                TestFixtureCi.ClearLogs();
            }
        }

        // Verifies that ABP confirmed and unconfirmed messages are working
        // Uses Device25_ABP and Device26_ABP
        [RetryFact]
        public async Task Test_ABP_Device_With_Connection_Timeout()
        {
            LogTestStart(new TestDeviceInfo[] { TestFixtureCi.Device25_ABP, TestFixtureCi.Device26_ABP });

            // Sends 1 message from device 25
            var device25 = TestFixtureCi.Device25_ABP;
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device25.DevAddr, device25.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device25.NwkSKey, device25.AppSKey, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);
            await ArduinoDevice.transferPacketAsync(GeneratePayloadMessage(), 10);

            await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith($"{device25.DeviceID}: processing time", StringComparison.Ordinal));

            // wait 61 seconds
            await Task.Delay(TimeSpan.FromSeconds(120));

            // Send 1 message from device 26
            var device26 = TestFixtureCi.Device26_ABP;
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device26.DevAddr, device26.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device26.NwkSKey, device26.AppSKey, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);
            await ArduinoDevice.transferPacketAsync(GeneratePayloadMessage(), 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            var result = await TestFixtureCi.SearchNetworkServerModuleAsync(
                        msg => msg.StartsWith($"{device25.DeviceID}: device client disconnected", StringComparison.Ordinal),
                        new SearchLogOptions
                        {
                            MaxAttempts = 10,
                            SourceIdFilter = device25.GatewayID
                        });

            Assert.NotNull(result.MatchedEvent);

            TestFixtureCi.ClearLogs();

            // Send 1 message from device 25 and check that connection was restablished
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device25.DevAddr, device25.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device25.NwkSKey, device25.AppSKey, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);
            var expectedMessage = GeneratePayloadMessage();
            await ArduinoDevice.transferPacketAsync(expectedMessage, 10);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

            // 0000000000000005: message '{"value": 51}' sent to hub
            await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device25.DeviceID}: message '{{\"value\":{expectedMessage}}}' sent to hub");

            // "device client reconnected"
            await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device25.DeviceID}: device client reconnected");
        }

        private static string GeneratePayloadMessage() => PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
    }
}
