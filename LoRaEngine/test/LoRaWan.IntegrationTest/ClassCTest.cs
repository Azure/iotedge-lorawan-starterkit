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
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices;
    using Newtonsoft.Json;
    using Xunit;

    // Tests Cloud to Device messages
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class ClassCTest : IntegrationTestBaseCi
    {
        public ClassCTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        // Ensures that class C devices can receive messages from a direct method call
        // Uses Device24_ABP
        [Theory]
        [InlineData(nameof(IntegrationTestFixtureCi.Device24_ABP))]
        [InlineData(nameof(IntegrationTestFixtureCi.Device33_ABP_MultiGw))]
        public async Task Test_ClassC_Send_Message_From_Direct_Method_Should_Be_Received(string deviceId)
        {
            var device = this.TestFixtureCi.GetDeviceByPropertyName(deviceId);
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);
            await this.ArduinoDevice.setClassTypeAsync(LoRaArduinoSerial._class_type_t.CLASS_C);

            // Send 1 message upstream to register region/preferred gateway
            var msg = PayloadGenerator.Next().ToString();
            this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' to register region/preferred gateway in class C device");
            await this.ArduinoDevice.transferPacketAsync(msg, 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

            // 0000000000000005: valid frame counter, msg: 1 server: 0
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

            // 0000000000000005: decoding with: DecoderValueSensor port: 8
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

            // 0000000000000005: message '{"value": 51}' sent to hub
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

            this.TestFixtureCi.ClearLogs();

            var c2d = new LoRaCloudToDeviceMessage()
            {
                DevEUI = device.DeviceID,
                MessageId = Guid.NewGuid().ToString(),
                Fport = 23,
                RawPayload = Convert.ToBase64String(new byte[] { 0xFF, 0x00 }),
            };

            // Call Azure Function to send cloud to device message
            Assert.True(await LoRaAPIHelper.SendCloudToDeviceMessageAsync(device.DeviceID, c2d));

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            Assert.Contains(this.ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: PORT: 23; RX: \"FF00\"", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(this.ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: RXWIN0, RSSI", StringComparison.OrdinalIgnoreCase));
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);
        }
    }
}