// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class FcntLimitTest : MessageProcessorTestBase
    {
        private readonly Mock<LoRaDeviceAPIServiceBase> loRaDeviceApi;
        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

        public FcntLimitTest()
        {
            this.loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
        }

        [Theory]
        // rolling test: if the client reaches 0xFFFF on the lower 16 bits, it will roll over and the upper
        // 16 bits are changed. In this case, the counter drifted too far for us to recover, and hence the MIC check will fail
        [InlineData(0xF000FFFF + Constants.MAX_FCNT_GAP + 2, 0xF000FFFF, 0U, null, null, 0xF000FFFF + 20U, 0, false, false, true, LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck)]
        // rolling test: if the client reaches 0xFFFF on the lower 16 bits, it will roll over and the upper
        // 16 bits are changed. Validate, that we can recover from that.
        [InlineData(0xF000FFFF + 20U, 0xF000FFFF, 0U, null, null, 0xF000FFFF + 20U, 0, false, false, true)]
        // abp relaxed false
        [InlineData(0, 1U, 0U, null, null, 0, 0, false, false, false, LoRaDeviceRequestFailedReason.InvalidFrameCounter)]
        // gap test
        [InlineData(ushort.MaxValue, ushort.MaxValue + 1U, 0U, null, null, 0, 0, false, false, false, LoRaDeviceRequestFailedReason.InvalidFrameCounter)]
        // frame up: 50, server side ushort.max + 1 (0) expected: ushort.max + 2
        [InlineData(50, ushort.MaxValue + 1U, 0U, null, null, ushort.MaxValue + 51U, 0, false, false)]
        // frame up: 1, server side ushort.max + 1 (0) expected: ushort.max + 2
        [InlineData(1, ushort.MaxValue + 1U, 0U, null, null, ushort.MaxValue + 2U, 0, false, false)]
        // start at 1, allow relaxed, validate up/down are incremented
        [InlineData(1, 0U, 0U, null, null, 1, 1, true, true)]
        public async Task Validate_Limits(
            uint payloadFcntUp,
            uint? deviceFcntUp,
            uint? deviceFcntDown,
            uint? startFcntUp,
            uint? startFcntDown,
            uint expectedFcntUp,
            uint expectedFcntDown,
            bool abpRelaxed,
            bool confirmed,
            bool supports32Bit = false,
            LoRaDeviceRequestFailedReason? failedReason = null)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID, supports32BitFcnt: supports32Bit));

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null)).ReturnsAsync(true);
            var initialTwin = SetupTwins(deviceFcntUp, deviceFcntDown, startFcntUp, startFcntDown, abpRelaxed, supports32Bit, simulatedDevice, devEUI, devAddr);

            this.LoRaDeviceClient
                .Setup(x => x.GetTwinAsync()).Returns(() =>
                {
                    return Task.FromResult(initialTwin);
                });

            uint? fcntUpSavedInTwin = null;
            uint? fcntDownSavedInTwin = null;

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Returns<TwinCollection>((t) =>
                {
                    fcntUpSavedInTwin = (uint)t[TwinProperty.FCntUp];
                    fcntDownSavedInTwin = (uint)t[TwinProperty.FCntDown];
                    return Task.FromResult(true);
                });

            this.LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var shouldReset = payloadFcntUp == 0 && abpRelaxed;
            if (shouldReset)
            {
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(devEUI, It.IsAny<uint>(), It.IsNotNull<string>())).ReturnsAsync(true);
            }

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            WaitableLoRaRequest req = null;

            if (confirmed)
            {
                var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: (uint)payloadFcntUp);
                var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
                req = this.CreateWaitableRequest(rxpk);
            }
            else
            {
                var rxpk = simulatedDevice.CreateUnconfirmedMessageUplink("1234", fcnt: (uint)payloadFcntUp).Rxpk[0];
                req = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            }

            messageDispatcher.DispatchRequest(req);
            Assert.True(await req.WaitCompleteAsync(-1));

            if (failedReason.HasValue)
            {
                Assert.Equal(failedReason.Value, req.ProcessingFailedReason);
            }
            else
            {
                Assert.True(req.ProcessingSucceeded);
                Assert.True(this.LoRaDeviceFactory.TryGetLoRaDevice(devEUI, out var loRaDevice));

                if (confirmed)
                {
                    Assert.NotNull(req.ResponseDownlink);
                    Assert.True(req.ProcessingSucceeded);
                    Assert.Single(this.PacketForwarder.DownlinkMessages);
                    var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
                    var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
                    payloadDataDown.PerformEncryption(simulatedDevice.AppSKey);
                    Assert.Equal(expectedFcntDown, payloadDataDown.GetFcnt());
                }

                Assert.Equal(expectedFcntUp, loRaDevice.FCntUp);
            }
        }

        [Theory]
        // not saving, reported match, reset counter is lower
        [InlineData(11, 10U, 10U, 10U, 10U, 1, 3, 10, 10, false)]
        // saving, reported match, but reset counter is set, reported null
        [InlineData(11, 10U, 10U, 10U, 10U, 1, null, 10, 10, true)]
        // saving, reported match, but reset counter is set
        [InlineData(11, 10U, 10U, 10U, 10U, 1, 0, 10, 10, true)]
        // save reporting do not match
        [InlineData(2, 1U, 1U, 0U, 0U, 0, 0, 1U, 1U, true)]
        // save reporting do not match
        [InlineData(11, 10U, 20U, 0U, 0U, 0, 0, 10U, 20U, true)]
        public async Task ValidateFcnt_Start_Values_And_ResetCounter (
            short fcntUp,
            uint startFcntUpDesired,
            uint startFcntDownDesired,
            uint? startFcntUpReported,
            uint? startFcntDownReported,
            int? fcntResetCounterDesired,
            int? fcntResetCounterReported,
            uint startUpExpected,
            uint startDownExpected,
            bool saveExpected)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID));

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null)).ReturnsAsync(true);

            var initialTwin = SetupTwins((uint)fcntUp, startFcntDownDesired, startFcntUpDesired, startFcntDownDesired, true, false, simulatedDevice, devEUI, devAddr);

            if (startFcntUpReported.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntUpStart] = startFcntUpReported.Value;
            if (startFcntDownReported.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntDownStart] = startFcntDownReported.Value;

            if (fcntResetCounterDesired.HasValue)
                initialTwin.Properties.Desired[TwinProperty.FCntResetCounter] = fcntResetCounterDesired.Value;
            if (fcntResetCounterReported.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntResetCounter] = fcntResetCounterReported.Value;

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(initialTwin);

            uint? fcntUpSavedInTwin = null;
            uint? fcntDownSavedInTwin = null;
            uint? fcntStartUpSavedInTwin = null;
            uint? fcntStartDownSavedInTwin = null;

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Returns<TwinCollection>((t) =>
                {
                    fcntUpSavedInTwin = (uint)t[TwinProperty.FCntUp];
                    fcntDownSavedInTwin = (uint)t[TwinProperty.FCntDown];
                    fcntStartUpSavedInTwin = (uint)t[TwinProperty.FCntUpStart];
                    fcntStartDownSavedInTwin = (uint)t[TwinProperty.FCntDownStart];

                    return Task.FromResult(true);
                });

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>())).ReturnsAsync((Message)null);
            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr)).ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var rxpk = simulatedDevice.CreateUnconfirmedMessageUplink("1234", fcnt: (uint)fcntUp).Rxpk[0];
            var req = new WaitableLoRaRequest(rxpk, this.PacketForwarder);

            messageDispatcher.DispatchRequest(req);
            await req.WaitCompleteAsync();

            if (saveExpected)
            {
                Assert.True(req.ProcessingSucceeded);
                this.LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()), Times.Exactly(1));
                Assert.Equal(fcntUpSavedInTwin, fcntStartUpSavedInTwin);
                Assert.Equal(fcntDownSavedInTwin, fcntStartDownSavedInTwin);
                Assert.Equal(startUpExpected, fcntStartUpSavedInTwin.Value);
                Assert.Equal(startDownExpected, fcntStartDownSavedInTwin.Value);
            }
            else
            {
                this.LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()), Times.Never());
            }
        }

        private static Twin SetupTwins(uint? deviceFcntUp, uint? deviceFcntDown, uint? startFcntUp, uint? startFcntDown, bool abpRelaxed, bool supports32Bit, SimulatedDevice simulatedDevice, string devEUI, string devAddr)
        {
            var initialTwin = new Twin();
            initialTwin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            initialTwin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            initialTwin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            initialTwin.Properties.Desired[TwinProperty.NwkSKey] = simulatedDevice.LoRaDevice.NwkSKey;
            initialTwin.Properties.Desired[TwinProperty.AppSKey] = simulatedDevice.LoRaDevice.AppSKey;
            initialTwin.Properties.Desired[TwinProperty.DevAddr] = devAddr;
            initialTwin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            initialTwin.Properties.Desired[TwinProperty.GatewayID] = simulatedDevice.LoRaDevice.GatewayID;
            initialTwin.Properties.Desired[TwinProperty.ABPRelaxMode] = abpRelaxed;

            initialTwin.Properties.Desired[TwinProperty.Supports32BitFCnt] = supports32Bit;

            if (deviceFcntUp.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntUp] = deviceFcntUp.Value;
            if (deviceFcntDown.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntDown] = deviceFcntDown.Value;
            if (startFcntUp.HasValue)
                initialTwin.Properties.Desired[TwinProperty.FCntUpStart] = startFcntUp.Value;
            if (startFcntDown.HasValue)
                initialTwin.Properties.Desired[TwinProperty.FCntDownStart] = startFcntDown.Value;
            return initialTwin;
        }
    }
}
