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
    using LoRaTools.CommonAPI;
    using LoRaWan.Test.Shared;
    using Newtonsoft.Json.Linq;
    using Xunit;

    // Tests OTAA requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class MultiGatewayTests : IntegrationTestBaseCi
    {
        public MultiGatewayTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [Fact]
        public async Task Test_MultiGW_OTTA_Join_Single()
        {
            var device = this.TestFixtureCi.Device27_OTAA;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // validate that one GW refused the join
            const string joinRefusedMsg = "join refused";
            var joinRefused = await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync((s) => s.IndexOf(joinRefusedMsg) != -1, new SearchLogOptions(joinRefusedMsg));
            Assert.True(joinRefused.Found);

            await this.TestFixtureCi.WaitForTwinSyncAfterJoinAsync(this.ArduinoDevice.SerialLogs, device.DeviceID);

            // expecting both gw to start picking up messages
            // and sending to IoT hub.
            var bothReported = false;
            for (var i = 0; i < 5; i++)
            {
                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                bothReported = await this.TestFixtureCi.ValidateMultiGatewaySources((log) => log.StartsWith($"{device.DeviceID}: sending message", StringComparison.OrdinalIgnoreCase));
                if (bothReported)
                {
                    break;
                }
            }

            Assert.True(bothReported);
        }

        [Theory]
        [InlineData("Device29_ABP", "Mark")]
        [InlineData("Device28_ABP", "Drop")]
        public async Task Test_Deduplication_Strategies(string devicePropertyName, string strategy)
        {
            var device = this.TestFixtureCi.GetDeviceByPropertyName(devicePropertyName);
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            for (int i = 0; i < 10; i++)
            {
                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketAsync(msg, 10);
                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                var allGwGotIt = await this.TestFixtureCi.ValidateMultiGatewaySources((log) => log.IndexOf($"deduplication Strategy: {strategy}", StringComparison.OrdinalIgnoreCase) != -1);
                if (allGwGotIt)
                {
                    var notDuplicate = "\"IsDuplicate\":false";
                    var isDuplicate = "\"IsDuplicate\":true";

                    var notDuplicateResult = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(notDuplicate) != -1);
                    var duplicateResult = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(isDuplicate) != -1);

                    Assert.NotNull(notDuplicateResult.MatchedEvent);
                    Assert.NotNull(duplicateResult.MatchedEvent);

                    Assert.NotEqual(duplicateResult.MatchedEvent.SourceId, notDuplicateResult.MatchedEvent.SourceId);

                    switch (strategy)
                    {
                        case "Mark":
                            await this.TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, "dupmsg", "true");
                            break;
                        case "Drop":
                            var logMsg = $"{device.DeviceID}: duplication strategy indicated to not process message";
                            var droppedLog = await this.TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(logMsg), new SearchLogOptions { Description = logMsg, SourceIdFilter = duplicateResult.MatchedEvent.SourceId });
                            Assert.NotNull(droppedLog.MatchedEvent);

                            var expectedPayload = $"{{\"value\":{msg}}}";
                            await this.TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);
                            break;
                    }
                }

                this.TestFixtureCi.ClearLogs();
            }
        }
    }
}