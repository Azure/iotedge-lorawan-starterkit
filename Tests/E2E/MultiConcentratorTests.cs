// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using Xunit;
    using XunitRetryHelper;

    // Tests multi-concentrator scenarios
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class MultiConcentratorTests : IntegrationTestBaseCi
    {
        public MultiConcentratorTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [RetryFact]
        public async Task Test_Concentrator_Deduplication_OTAA()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName("Device31_OTAA");
            LogTestStart(device);

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            const int MESSAGE_COUNT = 5;

            for (var i = 0; i < MESSAGE_COUNT; ++i)
            {
                Console.WriteLine($"Starting sending OTAA confirmed message {i + 1}/{MESSAGE_COUNT}");
                TestFixtureCi.ClearLogs();

                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                // 0000000000000031: message '{"value": 101}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                var logMsg = $"Duplicate message received from station with EUI";
                var droppedLog = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.IndexOf(logMsg, StringComparison.Ordinal) != -1);
                Assert.NotNull(droppedLog.MatchedEvent);

                TestFixtureCi.ClearLogs();
            }
        }
    }
}
