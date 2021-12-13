// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Xunit;
    using XunitRetryHelper;

    // Tests multi-concentrator scenarios
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class MultiConcentratorTests : IntegrationTestBaseCi, IAsyncLifetime
    {
        private string temporaryDirectoryName;
        public MultiConcentratorTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        public Task DisposeAsync()
        {
            TestUtils.KillBasicsStation(TestFixture.Configuration, this.temporaryDirectoryName);
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            TestUtils.StartBasicsStation(TestFixture.Configuration, new Dictionary<string, string>()
            {
                { "TLS_SNI", "false" },
                { "TC_URI", TestFixture.Configuration.SharedLnsEndpoint },
                { "FIXED_STATION_EUI", TestFixture.Configuration.DefaultBasicStationEui },
                { "RADIODEV", TestFixture.Configuration.RadioDev }
            }, out this.temporaryDirectoryName);
            // Waiting 5 seconds for the BasicsStation to connect
            await Task.Delay(5_000);
            var log = await TestFixtureCi.SearchNetworkServerModuleAsync(
                (log) => log.IndexOf(TestFixture.Configuration.DefaultBasicStationEui, StringComparison.Ordinal) != -1);
            Assert.NotNull(log.MatchedEvent);
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

            var droppedLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                (log) => log.IndexOf(NetworkServer.Constants.DuplicateMessageFromAnotherStationMsg, StringComparison.Ordinal) != -1);
            Assert.NotNull(droppedLog.MatchedEvent);

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            const int MESSAGE_COUNT = 5;

            for (var i = 0; i < MESSAGE_COUNT; ++i)
            {
                Log($"{device.DeviceID}: Sending OTAA confirmed message {i + 1}/{MESSAGE_COUNT}");

                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                // 0000000000000031: message '{"value": 101}' sent to hub
                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                droppedLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf(NetworkServer.Constants.DuplicateMessageFromAnotherStationMsg, StringComparison.Ordinal) != -1);
                Assert.NotNull(droppedLog.MatchedEvent);

                TestFixtureCi.ClearLogs();
            }
        }
    }
}
