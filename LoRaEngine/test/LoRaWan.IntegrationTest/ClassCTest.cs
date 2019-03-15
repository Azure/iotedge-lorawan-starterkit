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
        [Fact(Skip="Waiting for Edge Hub v1.0.7")]
        public async Task Test_ClassC_Send_Message_From_Direct_Method_Should_Be_Received()
        {
            const int MAX_MODULE_DIRECT_METHOD_CALL_TRIES = 3;
            var device = this.TestFixtureCi.Device24_ABP;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);
            await this.ArduinoDevice.setClassTypeAsync(LoRaArduinoSerial._class_type_t.CLASS_C);

            var c2d = new LoRaCloudToDeviceMessage()
            {
                DevEUI = device.DeviceID,
                MessageId = Guid.NewGuid().ToString(),
                Fport = 23,
                RawPayload = Convert.ToBase64String(new byte[] { 0xFF, 0x00 }),
            };

            TestLogger.Log($"[INFO] Using service client to call direct method to {this.TestFixture.Configuration.LeafDeviceGatewayID}/{this.TestFixture.Configuration.NetworkServerModuleID}");
            TestLogger.Log($"[INFO] {JsonConvert.SerializeObject(c2d, Formatting.None)}");

            for (var i = 0; i < MAX_MODULE_DIRECT_METHOD_CALL_TRIES;)
            {
                i++;

                try
                {
                    await this.TestFixtureCi.InvokeModuleDirectMethodAsync(this.TestFixture.Configuration.LeafDeviceGatewayID, this.TestFixture.Configuration.NetworkServerModuleID, "cloudtodevicemessage", c2d);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == MAX_MODULE_DIRECT_METHOD_CALL_TRIES)
                        throw;

                    TestLogger.Log($"[ERR] Failed to call module direct method: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            Assert.Contains(this.ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: PORT: 23; RX: \"FF00\""));
            Assert.Contains(this.ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: RXWIN0, RSSI"));
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);
        }
    }
}