// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
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
            var crcParseResult = uint.TryParse(TestFixture.Configuration.ClientBundleCrc, out var crc);
            try
            {
                var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device33_OTAA));
                LogTestStart(device, stationEui);

                if (!string.IsNullOrEmpty(clientThumbprint))
                {
                    //if a test re-run, clientThumbprint will be empty, therefore there's nothing to do, previously generated certificates will be reused
                    //update allowed client thumbprints in IoT Hub Twin to only have the one being added
                    await TestFixture.UpdateExistingConcentratorThumbprint(stationEui,
                                                                           condition: (originalArray) => !originalArray.Any(x => x.Equals(clientThumbprint, StringComparison.OrdinalIgnoreCase)),
                                                                           action: (originalList) => {
                                                                               originalList.RemoveAll(x => true); // remove all keys
                                                                               originalList.Add(clientThumbprint); // add only new thumbprint
                                                                           });
                }

                if (crcParseResult)
                {
                    //if a test re-run, crc field will be empty, therefore there's nothing to do, previously generated certificates will be reused
                    //update crc value with the one being generated in ci
                    await TestFixture.UpdateExistingConcentratorCrcValues(stationEui, crc);
                }

                //setup the concentrator with CUPS_URI only (certificates are retrieved from default location)
                TestUtils.StartBasicsStation(TestFixture.Configuration, new Dictionary<string, string>()
                {
                    { "TLS_SNI", "false" },
                    { "CUPS_URI", TestFixture.Configuration.SharedCupsEndpoint },
                    { "FIXED_STATION_EUI", stationEui },
                    { "RADIODEV", TestFixture.Configuration.RadioDev }
                }, out temporaryDirectoryName);

                // Waiting 30s for being sure that BasicStation actually started up
                await Task.Delay(30_000);

                var log = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf($"{stationEui}: Received 'version' message for station", StringComparison.Ordinal) != -1, new SearchLogOptions { MaxAttempts = 1 });
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
                    (log) => log.IndexOf($"{stationEui}: Received 'jreq' message", StringComparison.Ordinal) != -1, new SearchLogOptions { MaxAttempts = 2 });
                Assert.NotNull(jreqLog.MatchedEvent);

                // wait 1 second after joined
                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

                Log($"{device.DeviceID}: Sending OTAA unconfirmed message");

                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                var expectedPayload = $"{{\"value\":{msg}}}";
                await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload, new SearchLogOptions { MaxAttempts = 2 });

                var updfLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf($"{stationEui}: Received 'updf' message", StringComparison.Ordinal) != -1, new SearchLogOptions { MaxAttempts = 2 });
                Assert.True(updfLog.Found);
            }
            finally
            {
                TestUtils.KillBasicsStation(TestFixture.Configuration, temporaryDirectoryName, out var logFilePath);
                if (string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
                {
                    Log("[INFO] ** Basic Station Logs Start **");
                    Log(await File.ReadAllTextAsync(logFilePath));
                    Log("[INFO] ** Basic Station Logs End **");
                    File.Delete(logFilePath);
                }
            }
            TestFixtureCi.ClearLogs();
        }
    }
}
