// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml;
    using LoRaWan.Test.Shared;
    using Newtonsoft.Json.Linq;
    using Xunit;

    // Tests OTAA requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class OTAATest : IntegrationTestBaseCi
    {
        public OTAATest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        // Performs a OTAA join and sends N confirmed and unconfirmed messages
        // Expects that:
        // - device message is available on IoT Hub
        // - frame counter validation is done
        // - Message is decoded
        [Theory]
        [InlineData("Device4_OTAA")]
        [InlineData("Device4_OTAA_MultiGw")]
        public async Task Test_OTAA_Confirmed_And_Unconfirmed_Message_With_Custom_RX1_DR_Offset(string devicePropertyName)
        {
            var device = this.TestFixtureCi.GetDeviceByPropertyName(devicePropertyName);
            const int MESSAGES_COUNT = 10;

            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await this.TestFixtureCi.WaitForTwinSyncAfterJoinAsync(this.ArduinoDevice.SerialLogs, device.DeviceID);
            }

            // Sends 10x unconfirmed messages
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                Console.WriteLine($"Starting sending OTAA unconfirmed message {i + 1}/{MESSAGES_COUNT}");
                this.TestFixtureCi.ClearLogs();

                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // 0000000000000004: valid frame counter, msg: 1 server: 0
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000004: message '{"value": 51}' sent to hub
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            // Sends 10x confirmed messages
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                Console.WriteLine($"Starting sending OTAA confirmed message {i + 1}/{MESSAGES_COUNT}");
                this.TestFixtureCi.ClearLogs();

                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                if (device.IsMultiGw)
                {
                    // multi gw, make sure one ignored the message
                    var searchTokenSending = $"{device.DeviceID}: sending a downstream message";
                    var sending = await this.TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenSending, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(sending.MatchedEvent);

                    var searchTokenAlreadySent = $"{device.DeviceID}: another gateway has already sent ack or downlink msg";
                    var ignored = await this.TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(searchTokenAlreadySent, StringComparison.OrdinalIgnoreCase));
                    Assert.NotNull(ignored.MatchedEvent);

                    Assert.NotEqual(sending.MatchedEvent.SourceId, ignored.MatchedEvent.SourceId);
                }

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs, maxAttempts: 5, interval: TimeSpan.FromSeconds(10));

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000004: message '{"value": 51}' sent to hub
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                if (this.ArduinoDevice.SerialLogs.Where(x => x.StartsWith("+CMSG: RXWIN1")).Count() > 0)
                {
                    // Expect that the response is done on DR4 as the RX1 offset is 1 on this device.
                    await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync(log => log.Contains("\"datr\":\"SF8BW125\""), null);
                }

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }
        }
    }
}