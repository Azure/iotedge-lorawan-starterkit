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

        // Ensures that class C devices can receive messages from a direct method call
        // Uses Device24_ABP
        [Fact]
        public async Task Test_ClassC_Send_Message_From_Direct_Method_Should_Be_Received()
        {
            const int MAX_MODULE_DIRECT_METHOD_CALL_TRIES = 3;
            var device = TestFixtureCi.Device24_ABP;
            LogTestStart(device);

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion);
            await ArduinoDevice.setClassTypeAsync(LoRaArduinoSerial._class_type_t.CLASS_C);

            // send one confirmed message for ensuring that a basicstation is "bound" to the device
            var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
            Log($"{device.DeviceID}: Sending confirmed '{msg}'");
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

            // Now sending a c2d
            var c2d = new LoRaCloudToDeviceMessage()
            {
                DevEUI = device.DeviceID,
                MessageId = Guid.NewGuid().ToString(),
                Fport = 23,
                RawPayload = Convert.ToBase64String(new byte[] { 0xFF, 0x00 }),
            };

            TestLogger.Log($"[INFO] Using service client to call direct method to {TestFixture.Configuration.LeafDeviceGatewayID}/{TestFixture.Configuration.NetworkServerModuleID}");
            TestLogger.Log($"[INFO] {JsonConvert.SerializeObject(c2d, Formatting.None)}");

            for (var i = 0; i < MAX_MODULE_DIRECT_METHOD_CALL_TRIES; ++i)
            {
                try
                {
                    await TestFixtureCi.InvokeModuleDirectMethodAsync(TestFixture.Configuration.LeafDeviceGatewayID, TestFixture.Configuration.NetworkServerModuleID, "cloudtodevicemessage", c2d);
                    break;
                }
                catch (Exception ex)
                {
#pragma warning disable CA1508 // Avoid dead conditional code (false positive)
                    if (i == MAX_MODULE_DIRECT_METHOD_CALL_TRIES - 1) throw;
#pragma warning restore CA1508 // Avoid dead conditional code
                    TestLogger.Log($"[ERR] Failed to call module direct method: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            Assert.Contains(ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: PORT: 23; RX: \"FF00\"", StringComparison.Ordinal));
            Assert.Contains(ArduinoDevice.SerialLogs, (l) => l.StartsWith("+MSG: RXWIN0, RSSI", StringComparison.Ordinal));
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);
        }
    }
}
