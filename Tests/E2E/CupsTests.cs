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

    // Tests cups scenarios
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class CupsTests : IntegrationTestBaseCi
    {
        public CupsTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [RetryFact]
        public async Task Test_Concentrator_Can_Receive_Updates_Then_Connect_To_Lns_And_Receive_Messages()
        {
            //arrange
            var temporaryDirectoryName = string.Empty;
            var stationEui = TestFixture.Configuration.CupsBasicStationEui;
            var clientThumbprint = TestFixture.Configuration.ClientThumbprint;
            Assert.NotNull(clientThumbprint);
            try
            {
                var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device33_OTAA));
                LogTestStart(device, stationEui);

                //update allowed client thumbprints in IoT Hub Twin
                await TestFixture.UpdateExistingConcentratorThumbprint(stationEui,
                                                                       (originalList) => !originalList.Contains(clientThumbprint),
                                                                       (originalList) => originalList.Add(clientThumbprint));

                //setup the concentrator with CUPS_URI only (certificates are retrieved from default location)
                TestUtils.StartBasicsStation(TestFixture.Configuration, new Dictionary<string, string>()
                {
                    { "TLS_SNI", "false" },
                    { "CUPS_URI", TestFixture.Configuration.SharedCupsEndpoint },
                    { "FIXED_STATION_EUI", stationEui },
                    { "RADIODEV", TestFixture.Configuration.RadioDev }
                }, out temporaryDirectoryName);
                var log = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf(stationEui, StringComparison.Ordinal) != -1);
                Assert.True(log.Found);

                //the concentrator should be ready at this point to receive messages
                //if receiving 'updf' is succeeding, cups worked successfully
                await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
                await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
                await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

                await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

                var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
                Assert.True(joinSucceeded, "Join failed");

                var jreqLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf($"{stationEui}: Received 'jreq' message", StringComparison.Ordinal) != -1);
                Assert.NotNull(jreqLog.MatchedEvent);

                // wait 1 second after joined
                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

                Log($"{device.DeviceID}: Sending OTAA confirmed message");

                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                var expectedPayload = $"{{\"value\":{msg}}}";
                await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                var updfLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf($"{stationEui}: Received 'updf' message", StringComparison.Ordinal) != -1);
                Assert.True(updfLog.Found);

                TestFixtureCi.ClearLogs();
            }
            finally
            {
                TestUtils.KillBasicsStation(TestFixture.Configuration, temporaryDirectoryName);
                //cleanup newly added client thumbprint
                await TestFixture.UpdateExistingConcentratorThumbprint(stationEui,
                                                                       (originalList) => originalList.Contains(clientThumbprint),
                                                                       (originalList) => originalList.Remove(clientThumbprint));
            }
        }
    }
}
