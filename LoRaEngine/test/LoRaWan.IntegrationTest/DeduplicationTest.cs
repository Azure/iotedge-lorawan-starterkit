// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using LoRaWan.Test.Shared;
    using Newtonsoft.Json.Linq;
    using Xunit;

    // Tests OTAA requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class DeduplicationTest : IntegrationTestBaseCi
    {
        public DeduplicationTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [Fact]
        public async Task Test_Deduplication_Drop()
        {
            var device = this.TestFixtureCi.Device25_OTAA;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            var joinConfirm = this.ArduinoDevice.SerialLogs.FirstOrDefault(s => s.StartsWith("+JOIN: NetID"));
            Assert.NotNull(joinConfirm);

            // validate that one GW refused the join
            const string devNonceAlreadyUsed = "DevNonce already used by this device";
            var joinRefused = await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync((s) => s.IndexOf(devNonceAlreadyUsed) != -1, new SearchLogOptions(devNonceAlreadyUsed));
            Assert.True(joinRefused.Found);

            this.TestFixtureCi.ClearLogs();

            var devAddr = joinConfirm.Substring(joinConfirm.LastIndexOf(' ') + 1);
            devAddr = devAddr.Replace(":", string.Empty);

            // wait for the twins to be stored and published -> all GW need the same state
            const int DelayForJoinTwinStore = 20 * 1000;
            const string DevAddrProperty = "DevAddr";
            const int MaxRuns = 3;
            bool reported = false;
            for (var i = 0; i < MaxRuns && !reported; i++)
            {
                await Task.Delay(DelayForJoinTwinStore);

                var twins = await this.TestFixtureCi.GetTwinAsync(device.DeviceID);
                if (twins.Properties.Reported.Contains(DevAddrProperty))
                {
                    reported = devAddr.Equals(twins.Properties.Reported[DevAddrProperty].Value as string, StringComparison.InvariantCultureIgnoreCase);
                }
            }

            Assert.True(reported);

            var msg = PayloadGenerator.Next().ToString();
            await this.ArduinoDevice.transferPacketAsync(msg, 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

            var notDuplicate = "{\"isDuplicate\":false";
            var resultProcessed = await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync(logmsg => logmsg.IndexOf(notDuplicate) != -1, new SearchLogOptions(notDuplicate));
            var resultDroped = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf("duplication strategy indicated to not process message") != -1);

            Assert.NotEqual(resultProcessed.MatchedEvent.SourceId, resultDroped.MatchedEvent.SourceId);
        }
    }
}