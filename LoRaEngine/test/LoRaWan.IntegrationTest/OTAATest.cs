// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
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
        [Fact]
        public async Task Test_OTAA_Confirmed_And_Unconfirmed_Message_With_Custom_RX1_DR_Offset()
        {
            const int MESSAGES_COUNT = 10;

            var device = this.TestFixtureCi.Device4_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

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

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000004: message '{"value": 51}' sent to hub
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                // Expect that the response is done on DR4 as the RX1 offset is 1 on this device.
                await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync(log => log.Contains("\"datr\":\"SF8BW125\""), null);

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }
        }
    }
}