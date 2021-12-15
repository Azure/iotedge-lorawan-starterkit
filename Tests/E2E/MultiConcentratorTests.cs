// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
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
        private bool initializationSucceeded;
        public MultiConcentratorTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        public async Task DisposeAsync()
        {
            TestUtils.KillBasicsStation(TestFixture.Configuration, this.temporaryDirectoryName, out var logFilePath);
            if (!string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
            {
                Log("[INFO] ** Basic Station Logs Start **");
                Log(await File.ReadAllTextAsync(logFilePath));
                Log("[INFO] ** Basic Station Logs End **");
                File.Delete(logFilePath);
            }
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
            var log = await TestFixtureCi.SearchNetworkServerModuleAsync(
                (log) => log.IndexOf(TestFixture.Configuration.DefaultBasicStationEui, StringComparison.Ordinal) != -1);
            this.initializationSucceeded = log.Found;
        }

        [RetryFact]
        public async Task Test_Concentrator_Deduplication_OTAA()
        {
            Assert.True(this.initializationSucceeded);
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
                var expectedPayload = $"{{\"value\":{msg}}}";
                await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                droppedLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf(NetworkServer.Constants.DuplicateMessageFromAnotherStationMsg, StringComparison.Ordinal) != -1);
                Assert.NotNull(droppedLog.MatchedEvent);

                TestFixtureCi.ClearLogs();
            }
        }
    }
}
