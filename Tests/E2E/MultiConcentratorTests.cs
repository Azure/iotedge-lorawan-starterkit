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
        public async Task Test_Concentrator_Deduplication_ABP()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName("Device31_ABP");
            LogTestStart(device);

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            const int MESSAGE_COUNT = 5;

            for (var i = 0; i < MESSAGE_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);

                Log($"{device.DeviceID}: Sending confirmed '{msg}' {i + 1}/{MESSAGE_COUNT}");
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(2 * Constants.DELAY_BETWEEN_MESSAGES);

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
