// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;
    using Xunit;

    // Tests sensor decoding test (http, reflection)
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public class SensorDecodingTest : IntegrationTestBaseCi
    {
        public SensorDecodingTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        // Ensures that http sensor decoder decodes payload
        // Uses device Device11_OTAA
        [Fact]
        public async Task SensorDecoder_HttpBased_ValueSensorDecoder_DecodesPayload()
        {
            var device = this.TestFixtureCi.Device11_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            await this.ArduinoDevice.transferPacketWithConfirmedAsync("1234", 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // +CMSG: ACK Received
            await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

            // Find "0000000000000011: message '{"value":1234}' sent to hub" in network server logs
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":\"1234\"}}' sent to hub");

            this.TestFixtureCi.ClearLogs();
        }

        // Ensures that reflect based sensor decoder decodes payload
        // Uses Device12_OTAA
        [Fact]
        public async Task SensorDecoder_ReflectionBased_ValueSensorDecoder_DecodesPayload()
        {
            var device = this.TestFixtureCi.Device12_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            await this.ArduinoDevice.transferPacketWithConfirmedAsync("4321", 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // +CMSG: ACK Received
            await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

            // Find "0000000000000011: message '{"value":1234}' sent to hub" in network server logs
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":4321}}' sent to hub");

            this.TestFixtureCi.ClearLogs();
        }
    }
}