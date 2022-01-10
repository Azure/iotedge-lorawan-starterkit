// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Xunit;
    using XunitRetryHelper;

    // Tests OTAA requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class OTAATest : IntegrationTestBaseCi
    {
        public OTAATest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [RetryFact]
        public Task Test_OTAA_Confirmed_And_Unconfirmed_Message_With_Custom_RX1_DR_Offset_Single()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device4_OTAA));
            LogTestStart(device);
            return Test_OTAA_Confirmed_And_Unconfirmed_Message_With_Custom_RX1_DR_Offset(device);
        }

        [RetryFact]
        public Task Test_OTAA_Confirmed_And_Unconfirmed_Message_With_Custom_RX1_DR_Offset_MultiGw()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device4_OTAA_MultiGw));
            LogTestStart(device);
            return Test_OTAA_Confirmed_And_Unconfirmed_Message_With_Custom_RX1_DR_Offset(device);
        }

        // Performs a OTAA join and sends N confirmed and unconfirmed messages
        // Expects that:
        // - device message is available on IoT Hub
        // - frame counter validation is done
        // - Message is decoded
        private async Task Test_OTAA_Confirmed_And_Unconfirmed_Message_With_Custom_RX1_DR_Offset(TestDeviceInfo device)
        {
            const int MESSAGES_COUNT = 10;

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await TestFixtureCi.WaitForTwinSyncAfterJoinAsync(ArduinoDevice.SerialLogs, device.DeviceID);
            }

            // Sends 10x unconfirmed messages
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                Console.WriteLine($"Starting sending OTAA unconfirmed message {i + 1}/{MESSAGES_COUNT}");
                TestFixtureCi.ClearLogs();

                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // 0000000000000004: valid frame counter, msg: 1 server: 0
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000004: message '{"value": 51}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            // Sends 10x confirmed messages
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                Console.WriteLine($"Starting sending OTAA confirmed message {i + 1}/{MESSAGES_COUNT}");
                TestFixtureCi.ClearLogs();

                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                if (device.IsMultiGw)
                {
                    // multi gw, make sure one ignored the message
                    var searchTokenSending = $"{device.DeviceID}: sending message to station with EUI";
                    var sending = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenSending, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(sending.MatchedEvent);

                    var searchTokenAlreadySent = $"{device.DeviceID}: another gateway has already sent ack or downlink msg";
                    var ignored = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenAlreadySent, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(ignored.MatchedEvent);

                    Assert.NotEqual(sending.MatchedEvent.SourceId, ignored.MatchedEvent.SourceId);
                }

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs, maxAttempts: 5, interval: TimeSpan.FromSeconds(10));

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000004: message '{"value": 51}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                if (ArduinoDevice.SerialLogs.Any(x => x.StartsWith("+CMSG: RXWIN1", StringComparison.Ordinal)))
                {
                    // Expect that the response is done on DR4 as the RX1 offset is 1 on this device.
                    await TestFixtureCi.AssertNetworkServerModuleLogExistsAsync(log => log.Contains("\"DataRateRx1\":4", StringComparison.Ordinal), null);
                }

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }
        }
    }
}
