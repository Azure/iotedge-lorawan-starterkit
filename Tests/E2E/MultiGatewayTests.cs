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
        public Task Test_OTAA_Deduplication_Strategy_Drop()
        {
            return Test_Deduplication_Strategies(TestFixtureCi.Device27_OTAA, true);
        }

        [RetryFact]
        public Task Test_ABP_Deduplication_Strategy_Mark()
        {
            return Test_Deduplication_Strategies(TestFixtureCi.Device29_ABP, false);
        }

        [RetryFact]
        public Task Test_ABP_Deduplication_Strategy_Drop()
        {
            return Test_Deduplication_Strategies(TestFixtureCi.Device28_ABP, false);
        }

        private async Task Test_Deduplication_Strategies(TestDeviceInfo device, bool isOtaa)
        {
            LogTestStart(device);

            if (isOtaa)
            {
                await OtaaSetupAndJoinAssertAsync(device);
            }
            else
            {
                await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
                await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
                await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

                await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);
            }

            for (var i = 0; i < 10; i++)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                await ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);
                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", ArduinoDevice.SerialLogs);

                var allGwGotIt = await TestFixtureCi.ValidateMultiGatewaySources((log) => log.IndexOf($"deduplication Strategy: {device.Deduplication}", StringComparison.OrdinalIgnoreCase) != -1);
                if (allGwGotIt)
                {
                    var notDuplicate = "\"IsDuplicate\":false";
                    var isDuplicate = "\"IsDuplicate\":true";

                    var notDuplicateResult = await TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(notDuplicate, StringComparison.Ordinal) != -1, new SearchLogOptions(notDuplicate));
                    var duplicateResult = await TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(isDuplicate, StringComparison.Ordinal) != -1, new SearchLogOptions(isDuplicate));

                    Assert.NotNull(notDuplicateResult.MatchedEvent);
                    Assert.NotNull(duplicateResult.MatchedEvent);

                    Assert.NotEqual(duplicateResult.MatchedEvent.SourceId, notDuplicateResult.MatchedEvent.SourceId);

                    switch (device.Deduplication)
                    {
                        case NetworkServer.DeduplicationMode.Mark:
                            var expectedProperty = "dupmsg";
                            await TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedProperty, "true", new SearchLogOptions(expectedProperty) { SourceIdFilter = duplicateResult.MatchedEvent.SourceId, TreatAsError = true });
                            await TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedProperty, "true", new SearchLogOptions(expectedProperty) { SourceIdFilter = notDuplicateResult.MatchedEvent.SourceId, TreatAsError = true });
                            break;
                        case NetworkServer.DeduplicationMode.Drop:
                            var logMsg = $"{device.DeviceID}: duplication strategy indicated to not process message";
                            var droppedLog = await TestFixtureCi.SearchNetworkServerModuleAsync((log) => log.StartsWith(logMsg, StringComparison.Ordinal), new SearchLogOptions(logMsg) { SourceIdFilter = duplicateResult.MatchedEvent.SourceId });
                            Assert.NotNull(droppedLog.MatchedEvent);

                            var expectedPayload = $"{{\"value\":{msg}}}";
                            await TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload, new SearchLogOptions(expectedPayload) { TreatAsError = true });
                            break;
                        case NetworkServer.DeduplicationMode.None:
                        default:
                            throw new SwitchExpressionException();
                    }
                }

                TestFixtureCi.ClearLogs();
            }
        }

        private async Task OtaaSetupAndJoinAssertAsync(TestDeviceInfo device)
        {
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEui);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // validate that one GW refused the join
            const string joinRefusedMsg = "join refused";
            var joinRefused = await TestFixtureCi.AssertNetworkServerModuleLogExistsAsync((s) => s.IndexOf(joinRefusedMsg, StringComparison.Ordinal) != -1, new SearchLogOptions(joinRefusedMsg));
            Assert.True(joinRefused.Found);

            await TestFixtureCi.WaitForTwinSyncAfterJoinAsync(ArduinoDevice.SerialLogs, device.DevEui);
        }
    }
}
