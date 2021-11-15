// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Xunit;
    using XunitRetryHelper;

    // Tests OTAA requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class MultiGatewayTests : IntegrationTestBaseCi
    {
        public MultiGatewayTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [RetryFact]
        public async Task Test_MultiGW_OTTA_Join_Single()
        {
            var device = TestFixtureCi.Device27_OTAA;
            LogTestStart(device);

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // validate that one GW refused the join
            const string joinRefusedMsg = "join refused";
            var joinRefused = await TestFixtureCi.AssertNetworkServerModuleLogExistsAsync((s) => s.IndexOf(joinRefusedMsg, StringComparison.Ordinal) != -1, new SearchLogOptions(joinRefusedMsg));
            Assert.True(joinRefused.Found);

            await TestFixtureCi.WaitForTwinSyncAfterJoinAsync(ArduinoDevice.SerialLogs, device.DeviceID);

            // expecting both gw to start picking up messages
            // and sending to IoT hub.
            var bothReported = false;
            for (var i = 0; i < 5; i++)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                var expectedPayload = $"{{\"value\":{msg}}}";
                await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                bothReported = await TestFixtureCi.ValidateMultiGatewaySources((log) => log.StartsWith($"{device.DeviceID}: sending message", StringComparison.OrdinalIgnoreCase));
                if (bothReported)
                {
                    break;
                }
            }

            Assert.True(bothReported);
        }

        [RetryFact]
        public Task Test_Deduplication_Strategies_Mark()
        {
            return Test_Deduplication_Strategies("Device29_ABP", "Mark");
        }

        [RetryFact]
        public Task Test_Deduplication_Strategies_Drop()
        {
            return Test_Deduplication_Strategies("Device28_ABP", "Drop");
        }

        private async Task Test_Deduplication_Strategies(string devicePropertyName, string strategy)
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(devicePropertyName);
            LogTestStart(device);

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            for (var i = 0; i < 10; i++)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketAsync(msg, 10);
                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                var allGwGotIt = await TestFixtureCi.ValidateMultiGatewaySources((log) => log.IndexOf($"deduplication Strategy: {strategy}", StringComparison.OrdinalIgnoreCase) != -1);
                if (allGwGotIt)
                {
                    var notDuplicate = "\"IsDuplicate\":false";
                    var isDuplicate = "\"IsDuplicate\":true";

                    var notDuplicateResult = await TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(notDuplicate, StringComparison.Ordinal) != -1);
                    var duplicateResult = await TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(isDuplicate, StringComparison.Ordinal) != -1);

                    Assert.NotNull(notDuplicateResult.MatchedEvent);
                    Assert.NotNull(duplicateResult.MatchedEvent);

                    Assert.NotEqual(duplicateResult.MatchedEvent.SourceId, notDuplicateResult.MatchedEvent.SourceId);

                    switch (strategy)
                    {
                        case "Mark":
                            await TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, "dupmsg", "true");
                            break;
                        case "Drop":
                            var logMsg = $"{device.DeviceID}: duplication strategy indicated to not process message";
                            var droppedLog = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(logMsg, StringComparison.Ordinal), new SearchLogOptions { Description = logMsg, SourceIdFilter = duplicateResult.MatchedEvent.SourceId });
                            Assert.NotNull(droppedLog.MatchedEvent);

                            var expectedPayload = $"{{\"value\":{msg}}}";
                            await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);
                            break;
                        default:
                            throw new SwitchExpressionException();
                    }
                }

                TestFixtureCi.ClearLogs();
            }
        }
    }
}
