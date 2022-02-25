// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class FcntLimitTest : MessageProcessorTestBase
    {
        public FcntLimitTest(ITestOutputHelper testOutputHelper) :
            base(testOutputHelper) { }

        [Theory]
        // rolling test: if the client reaches 0xFFFF on the lower 16 bits, it will roll over and the upper
        // 16 bits are changed. In this case, the counter drifted too far for us to recover, and hence the MIC check will fail
        [InlineData(0xF000FFFF + Constants.MaxFcntGap + 2, 0xF000FFFF, 0U, null, null, 0xF000FFFF + 20U, 0, false, false, true, LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck)]
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
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID, supports32BitFcnt: supports32Bit));

            var devEui = simulatedDevice.LoRaDevice.DevEui;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr.Value;

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null)).ReturnsAsync(true);

            var initialTwin = LoRaDeviceTwin.Create(
                simulatedDevice.LoRaDevice.GetAbpDesiredTwinProperties() with
                {
                    DevEui = devEui,
                    AbpRelaxMode = abpRelaxed,
                    Supports32BitFCnt = supports32Bit,
                    FCntUpStart = startFcntUp,
                    FCntDownStart = startFcntDown
                },
                new LoRaReportedTwinProperties
                {
                    FCntUp = deviceFcntUp,
                    FCntDown = deviceFcntDown,
                });

            LoRaDeviceClient
                .Setup(x => x.GetTwinAsync(CancellationToken.None)).Returns(() =>
                {
                    return Task.FromResult(initialTwin);
                });

            uint? fcntUpSavedInTwin = null;
            uint? fcntDownSavedInTwin = null;

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Returns<TwinCollection, CancellationToken>((t, _) =>
                {
                    fcntUpSavedInTwin = (uint)t[TwinProperty.FCntUp];
                    fcntDownSavedInTwin = (uint)t[TwinProperty.FCntDown];
                    return Task.FromResult(true);
                });

            LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var shouldReset = payloadFcntUp == 0 && abpRelaxed;
            if (shouldReset)
            {
                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(devEui, It.IsAny<uint>(), It.IsNotNull<string>())).ReturnsAsync(true);
            }

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "abc").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageDispatcher = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var loRaPayloadData = confirmed
                ? simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: payloadFcntUp, appSKey: simulatedDevice.AppSKey, nwkSKey: simulatedDevice.NwkSKey)
                : simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcntUp);

            using var req = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), loRaPayloadData);

            messageDispatcher.DispatchRequest(req);
            Assert.True(await req.WaitCompleteAsync(-1));

            if (failedReason.HasValue)
            {
                Assert.Equal(failedReason.Value, req.ProcessingFailedReason);
            }
            else
            {
                Assert.True(req.ProcessingSucceeded);
                Assert.True(DeviceCache.TryGetByDevEui(devEui, out var loRaDevice));

                if (confirmed)
                {
                    Assert.NotNull(req.ResponseDownlink);
                    Assert.True(req.ProcessingSucceeded);
                    Assert.Single(DownstreamMessageSender.DownlinkMessages);
                    var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
                    var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
                    payloadDataDown.Serialize(simulatedDevice.AppSKey.Value);
                    Assert.Equal(expectedFcntDown, payloadDataDown.Fcnt);
                }

                Assert.Equal(expectedFcntUp, loRaDevice.FCntUp);
            }
        }

        [Theory]
        // not saving, reported match, reset counter is lower
        [InlineData(11, 10U, 10U, 10U, 10U, 1U, 3U, 10, 10, false)]
        // saving, reported match, but reset counter is set, reported null
        [InlineData(11, 10U, 10U, 10U, 10U, 1U, null, 10, 10, true)]
        // saving, reported match, but reset counter is set
        [InlineData(11, 10U, 10U, 10U, 10U, 1U, 0U, 10, 10, true)]
        // save reporting do not match
        [InlineData(2, 1U, 1U, 0U, 0U, 0U, 0U, 1U, 1U, true)]
        // save reporting do not match
        [InlineData(11, 10U, 20U, 0U, 0U, 0U, 0U, 10U, 20U, true)]
        public async Task ValidateFcnt_Start_Values_And_ResetCounter(
            short fcntUp,
            uint startFcntUpDesired,
            uint startFcntDownDesired,
            uint? startFcntUpReported,
            uint? startFcntDownReported,
            uint? fcntResetCounterDesired,
            uint? fcntResetCounterReported,
            uint startUpExpected,
            uint startDownExpected,
            bool saveExpected)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID));

            var devEui = simulatedDevice.LoRaDevice.DevEui;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr.Value;

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null)).ReturnsAsync(true);

            var initialTwin = LoRaDeviceTwin.Create(
                simulatedDevice.LoRaDevice.GetAbpDesiredTwinProperties() with
                {
                    DevEui = devEui,
                    AbpRelaxMode = true,
                    Supports32BitFCnt = false,
                    FCntUpStart = startFcntUpDesired,
                    FCntDownStart = startFcntDownDesired,
                    FCntResetCounter = fcntResetCounterDesired
                },
                new LoRaReportedTwinProperties
                {
                    FCntUp = (uint)fcntUp,
                    FCntDown = startFcntDownDesired,
                    FCntUpStart = startFcntUpReported,
                    FCntDownStart = startFcntDownReported,
                    FCntResetCounter = fcntResetCounterReported
                });

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(initialTwin);

            uint? fcntUpSavedInTwin = null;
            uint? fcntDownSavedInTwin = null;
            uint? fcntStartUpSavedInTwin = null;
            uint? fcntStartDownSavedInTwin = null;

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Returns<TwinCollection, CancellationToken>((t, _) =>
                {
                    fcntUpSavedInTwin = (uint)t[TwinProperty.FCntUp];
                    fcntDownSavedInTwin = (uint)t[TwinProperty.FCntDown];
                    fcntStartUpSavedInTwin = (uint)t[TwinProperty.FCntUpStart];
                    fcntStartDownSavedInTwin = (uint)t[TwinProperty.FCntDownStart];

                    return Task.FromResult(true);
                });

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>())).ReturnsAsync((Message)null);
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr)).ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "abc").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageDispatcher = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: (uint)fcntUp);

            using var req = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), payload);

            messageDispatcher.DispatchRequest(req);
            await req.WaitCompleteAsync();

            if (saveExpected)
            {
                Assert.True(req.ProcessingSucceeded);
                LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
                Assert.Equal(fcntUpSavedInTwin, fcntStartUpSavedInTwin);
                Assert.Equal(fcntDownSavedInTwin, fcntStartDownSavedInTwin);
                Assert.Equal(startUpExpected, fcntStartUpSavedInTwin.Value);
                Assert.Equal(startDownExpected, fcntStartDownSavedInTwin.Value);
            }
            else
            {
                LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Never());
            }
        }
    }
}
