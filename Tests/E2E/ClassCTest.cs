// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
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

        // Ensures that class C devices can receive messages from a direct method call;
        // the test uses the SendCloudToDeviceMessage endpoint in LoRaKeysManagerFacade
        // instead of callling direct method from the test code.
        // Uses Device24_ABP
        [Fact]
        public async Task Test_ClassC_Send_Message_Using_Function_Endpoint_Should_Be_Received()
        {
            var device = TestFixtureCi.Device24_ABP;
            LogTestStart(device);

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);
            await ArduinoDevice.setClassTypeAsync(LoRaArduinoSerial._class_type_t.CLASS_C);

            // send one confirmed message for ensuring that a basicstation is "bound" to the device
            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
            Log($"{device.DeviceID}: Sending confirmed '{msg}'");
            await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);
            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            TestFixtureCi.ClearLogs();

            // Now sending a c2d
            var c2d = new LoRaCloudToDeviceMessage()
            {
                DevEUI = device.DeviceID,
                MessageId = Guid.NewGuid().ToString(),
                Fport = FramePorts.App23,
                RawPayload = Convert.ToBase64String(new byte[] { 0xFF, 0x00 }),
            };

            TestLogger.Log($"[INFO] Using service API to send C2D message to device {device.DeviceID}");
            TestLogger.Log($"[INFO] {JsonConvert.SerializeObject(c2d, Formatting.None)}");

            // send message using the SendCloudToDeviceMessage API endpoint
            Assert.True(await LoRaAPIHelper.SendCloudToDeviceMessage(device.DeviceID, c2d));

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            Assert.Contains(ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: PORT: 23; RX: \"FF00\"", StringComparison.Ordinal));
            Assert.Contains(ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: RXWIN0, RSSI", StringComparison.Ordinal));
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);
        }
    }
}
