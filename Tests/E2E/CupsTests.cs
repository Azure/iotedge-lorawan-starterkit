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
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Xunit;

    // Tests cups scenarios
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class CupsTests : IntegrationTestBaseCi
    {
        public CupsTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [Fact]
        public async Task Test_Concentrator_Can_Receive_Updates_Then_Connect_To_Lns_And_Receive_Messages()
        {
            //arrange
            var temporaryDirectoryName = string.Empty;
            var stationEui = StationEui.Parse(TestFixture.Configuration.CupsBasicStationEui);
            var clientThumbprint = TestFixture.Configuration.ClientThumbprint;
            var crcParseResult = uint.TryParse(TestFixture.Configuration.ClientBundleCrc, out var crc);
            var sigCrcParseResult = uint.TryParse(TestFixture.Configuration.CupsSigKeyChecksum, out var sigCrc);
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
                                                                           action: (originalList) =>
                                                                           {
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

                var fwDigest = TestFixture.Configuration.CupsFwDigest;
                var fwPackage = TestFixture.Configuration.CupsBasicStationPackage;
                var fwUrl = TestFixture.Configuration.CupsFwUrl;
                if (sigCrcParseResult && !string.IsNullOrEmpty(fwDigest) && !string.IsNullOrEmpty(fwPackage) && fwUrl is not null)
                {
                    //if a test re-run, the fields will be empty, therefore there's no update to achieve
                    await TestFixture.UpdateExistingFirmwareUpgradeValues(stationEui, sigCrc, fwDigest, fwPackage, fwUrl);
                }

                //setup the concentrator with CUPS_URI only (certificates are retrieved from default location)
                TestUtils.StartBasicsStation(TestFixture.Configuration, new Dictionary<string, string>()
                {
                    { "TLS_SNI", "false" },
                    { "CUPS_URI", TestFixture.Configuration.SharedCupsEndpoint },
                    { "FIXED_STATION_EUI", stationEui.ToString() },
                    { "RADIODEV", TestFixture.Configuration.RadioDev }
                }, out temporaryDirectoryName);

                // Waiting 30s for being sure that BasicStation actually started up
                await Task.Delay(30_000);

                // If package log does not match, firmware upgrade process failed
                var expectedLog = stationEui + $": Received 'version' message for station '{TestFixture.Configuration.CupsBasicStationVersion}' with package '{fwPackage}'";
                var log = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf(expectedLog, StringComparison.Ordinal) != -1, new SearchLogOptions(expectedLog) { MaxAttempts = 1 });
                Assert.True(log.Found);

                //the concentrator should be ready at this point to receive messages
                //if receiving 'updf' is succeeding, cups worked successfully
                await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
                await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEui);
                await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

                await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

                var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
                Assert.True(joinSucceeded, "Join failed");

                var expectedLog2 = stationEui + ": Received 'jreq' message";
                var jreqLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf(expectedLog2, StringComparison.Ordinal) != -1, new SearchLogOptions(expectedLog2) { MaxAttempts = 2 });
                Assert.NotNull(jreqLog.MatchedEvent);

                // wait 1 second after joined
                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

                Log($"{device.DeviceID}: Sending OTAA unconfirmed message");

                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                var expectedLog3 = $"{{\"value\":{msg}}}";
                await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedLog3, new SearchLogOptions(expectedLog3) { MaxAttempts = 2 });

                var expectedLog4 = stationEui + ": Received 'updf' message";
                var updfLog = await TestFixtureCi.SearchNetworkServerModuleAsync(
                    (log) => log.IndexOf(expectedLog4, StringComparison.Ordinal) != -1, new SearchLogOptions(expectedLog4) { MaxAttempts = 2 });
                Assert.True(updfLog.Found);

                var twin = await TestFixture.GetTwinAsync(stationEui.ToString());
                var twinReader = new TwinPropertiesReader(twin.Properties.Reported, null);
                Assert.True(twinReader.TryRead<string>(TwinProperty.Package, out var reportedPackage)
                            && string.Equals(fwPackage, reportedPackage, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                TestUtils.KillBasicsStation(TestFixture.Configuration, temporaryDirectoryName, out var logFilePath);
                if (!string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
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
