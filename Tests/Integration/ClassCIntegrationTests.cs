// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Class C device tests
    public class ClassCIntegrationTests : MessageProcessorTestBase
    {
        private readonly ITestOutputHelper testOutputHelper;

        public ClassCIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public static TheoryData<string, uint, uint, Region> Upstream_And_Downstream_Succeeds_For_All_Regions_TheoryData() => TheoryDataFactory.From(Upstream_And_Downstream_Succeeds_For_All_Regions_InternalTheoryData());

        private static IEnumerable<(string, uint, uint, Region)> Upstream_And_Downstream_Succeeds_For_All_Regions_InternalTheoryData()
        {
            var testData = new List<(string, uint, uint)>
            {
                (null, 0U, 0U),
                (null, 0U, 9U),
                (null, 5U, 5U),
                (null, 5U, 14U),
                (ServerGatewayID, 0U, 0U),
                (ServerGatewayID, 0U, 19U),
                (ServerGatewayID, 5U, 5U),
                (ServerGatewayID, 5U, 24U)
            };

            foreach (var regionType in new[] {
                RegionManager.EU868, RegionManager.US915, RegionManager.CN470RP1, RegionManager.CN470RP2 })
            {
                foreach (var (deviceGatewayId, fcntDownFromTwin, fcntDelta) in testData)
                    yield return (deviceGatewayId, fcntDownFromTwin, fcntDelta, regionType);
            }
        }

        [Theory]
#pragma warning disable CA1825 // Avoid zero-length array allocations
        [MemberData(nameof(Upstream_And_Downstream_Succeeds_For_All_Regions_TheoryData))]
#pragma warning restore CA1825 // Avoid zero-length array allocations
        public async Task When_ABP_Sends_Upstream_Followed_By_DirectMethod_Should_Send_Upstream_And_Downstream(string deviceGatewayID, uint fcntDownFromTwin, uint fcntDelta, Region region)
        {
            const uint payloadFcnt = 2; // to avoid relax mode reset

            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID, deviceClassType: LoRaDeviceClassType.C), frmCntDown: fcntDownFromTwin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var twin = LoRaDeviceTwin.Create(simDevice.LoRaDevice.GetAbpDesiredTwinProperties(),
                                             simDevice.GetAbpReportedTwinProperties() with { Region = region.LoRaRegion });

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr.Value))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            if (deviceGatewayID == null)
            {
                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                    .ReturnsAsync(new FunctionBundlerResult());
            }

            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);
            var payloadData = simDevice.CreateUnconfirmedDataUpMessage("1", fcnt: payloadFcnt);

            using var request = CreateWaitableRequest(payloadData, region: region);
            request.SetStationEui(new StationEui(ulong.MaxValue));
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // wait until cache has been updated
            await Task.Delay(50);

            // Adds fcntdown to device, simulating multiple downstream calls
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            loRaDevice.SetFcntDown(fcntDelta + loRaDevice.FCntDown);

            var classCSender = new DefaultClassCDevicesMessageSender(
                ServerConfiguration,
                deviceRegistry,
                DownstreamMessageSender,
                FrameCounterUpdateStrategyProvider,
                new TestOutputLogger<DefaultClassCDevicesMessageSender>(this.testOutputHelper),
                TestMeter.Instance);

            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = simDevice.DevEUI,
                MessageId = Guid.NewGuid().ToString(),
                Payload = "aaaa",
                Fport = FramePorts.App18,
            };

            var expectedFcntDown = fcntDownFromTwin + Constants.MaxFcntUnsavedDelta + fcntDelta;

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(simDevice.DevEUI, fcntDownFromTwin + fcntDelta, simDevice.FrmCntUp, ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)expectedFcntDown);
            }

            Assert.True(await classCSender.SendAsync(c2d));
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downstreamMsg = DownstreamMessageSender.DownlinkMessages[0];

            var downstreamPayloadBytes = downstreamMsg.Data;
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(expectedFcntDown, downstreamPayload.Fcnt);
            Assert.Equal(c2d.Fport, downstreamPayload.Fport);
            Assert.Equal(downstreamPayload.DevAddr, simDevice.DevAddr);
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey.Value);
            Assert.Equal(c2d.Payload, Encoding.UTF8.GetString(decryptedPayload));

            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(payloadFcnt, loRaDevice.FCntUp);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(ServerGatewayID)]
        public async Task When_OTAA_Join_Then_Sends_Upstream_DirectMethod_Should_Send_Downstream(string deviceGatewayID)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: deviceGatewayID));

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(LoRaDeviceTwin.Create(simDevice.LoRaDevice.GetOtaaDesiredTwinProperties(),
                                                    simDevice.GetOtaaReportedTwinProperties()));

            AppSessionKey? savedAppSKey = null;
            NetworkSessionKey? savedNwkSKey = null;
            var savedDevAddr = string.Empty;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection, CancellationToken>((t, _) =>
                {
                    savedAppSKey = AppSessionKey.Parse(t[TwinProperty.AppSKey].Value);
                    savedNwkSKey = NetworkSessionKey.Parse(t[TwinProperty.NwkSKey].Value);
                    savedDevAddr = t[TwinProperty.DevAddr];
                });

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            if (deviceGatewayID == null)
            {
                LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                    .ReturnsAsync(new FunctionBundlerResult());
            }

            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, simDevice.DevEUI, It.IsAny<DevNonce>()))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payloadata = simDevice.CreateJoinRequest();
            using var joinRequest = CreateWaitableRequest(payloadata);
            joinRequest.SetStationEui(new StationEui(ulong.MaxValue));
            messageDispatcher.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded);

            Assert.NotNull(savedAppSKey);
            Assert.NotNull(savedNwkSKey);
            Assert.NotEmpty(savedDevAddr);
            simDevice.SetupJoin(savedAppSKey.Value, savedNwkSKey.Value, DevAddr.Parse(savedDevAddr));
            using var request = CreateWaitableRequest(simDevice.CreateUnconfirmedDataUpMessage("1"));
            request.SetStationEui(new StationEui(ulong.MaxValue));
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            var classCSender = new DefaultClassCDevicesMessageSender(
                ServerConfiguration,
                deviceRegistry,
                DownstreamMessageSender,
                FrameCounterUpdateStrategyProvider,
                new TestOutputLogger<DefaultClassCDevicesMessageSender>(this.testOutputHelper),
                TestMeter.Instance);

            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = simDevice.DevEUI,
                MessageId = Guid.NewGuid().ToString(),
                Payload = "aaaa",
                Fport = FramePorts.App14,
            };

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(simDevice.DevEUI, simDevice.FrmCntDown, simDevice.FrmCntUp, ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)(simDevice.FrmCntDown + 1));
            }

            Assert.True(await classCSender.SendAsync(c2d));
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);
            var downstreamMsg = DownstreamMessageSender.DownlinkMessages[1];

            TestLogger.Log($"appSKey: {simDevice.AppSKey}, nwkSKey: {simDevice.NwkSKey}");

            var downstreamPayloadBytes = downstreamMsg.Data;
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(1, downstreamPayload.Fcnt);
            Assert.Equal(c2d.Fport, downstreamPayload.Fport);
            Assert.Equal(downstreamPayload.DevAddr, DevAddr.Parse(savedDevAddr));
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey.Value);
            Assert.Equal(c2d.Payload, Encoding.UTF8.GetString(decryptedPayload));

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        [Fact]
        public async Task Unconfirmed_Cloud_To_Device_From_Decoder_Should_Call_ClassC_Message_Sender()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;
            var devEui = new DevEui(2);

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var decoderResult = new DecodePayloadResult("1")
            {
                CloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
                {
                    Fport = FramePorts.App1,
                    MessageId = "123",
                    Payload = "12",
                    DevEUI = devEui,
                },
            };

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>(MockBehavior.Strict);
            payloadDecoder.Setup(x => x.DecodeMessageAsync(simulatedDevice.DevEUI, It.IsNotNull<byte[]>(), FramePorts.App1, It.IsAny<string>()))
                .ReturnsAsync(decoderResult);
            PayloadDecoder.SetDecoder(payloadDecoder.Object);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            using var c2dMessageSent = new SemaphoreSlim(0);
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.Is<IReceivedLoRaCloudToDeviceMessage>(m => !m.Confirmed && m.DevEUI == devEui), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Callback<IReceivedLoRaCloudToDeviceMessage, CancellationToken>((m, _) => c2dMessageSent.Release());
            RequestHandlerImplementation.SetClassCMessageSender(classCMessageSender.Object);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. No downstream message for the current device is sent
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is unchanged
            Assert.Equal(InitialDeviceFcntDown, loraDevice.FCntDown);

            // 6. Frame count has pending changes
            Assert.True(loraDevice.HasFrameCountChanges);

            // Ensure the message was sent
            Assert.True(await c2dMessageSent.WaitAsync(10 * 1000));

            payloadDecoder.VerifyAll();
            classCMessageSender.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task When_Joining_Should_Save_Region_And_Preferred_Gateway(
            [CombinatorialValues(null, ServerGatewayID)] string deviceGatewayID,
            [CombinatorialValues(null, ServerGatewayID, "another-gateway")] string initialPreferredGatewayID,
            [CombinatorialValues(null, LoRaRegionType.EU868, LoRaRegionType.US915)] LoRaRegionType? initialLoRaRegion)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: deviceGatewayID));

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(LoRaDeviceTwin.Create(simDevice.LoRaDevice.GetOtaaDesiredTwinProperties(),
                                                    new LoRaReportedTwinProperties
                                                    {
                                                        // reported: { 'PreferredGateway': '' } -> if device is for multiple gateways and one initial was defined
                                                        PreferredGatewayId = string.IsNullOrEmpty(deviceGatewayID) ? initialPreferredGatewayID : null,
                                                        Region = initialLoRaRegion
                                                    }));

            var shouldSavePreferredGateway = string.IsNullOrEmpty(deviceGatewayID) && initialPreferredGatewayID != ServerGatewayID;
            var shouldSaveRegion = !initialLoRaRegion.HasValue || initialLoRaRegion.Value != LoRaRegionType.EU868;

            var savedAppSKey = string.Empty;
            var savedNwkSKey = string.Empty;
            var savedDevAddr = string.Empty;
            TwinCollection actualTwinCollection = null;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection, CancellationToken>((t, _) =>
                {
                    savedAppSKey = t[TwinProperty.AppSKey];
                    savedNwkSKey = t[TwinProperty.NwkSKey];
                    savedDevAddr = t[TwinProperty.DevAddr];
                    actualTwinCollection = t;
                });

            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, simDevice.DevEUI, It.IsAny<DevNonce>()))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var joinPayload = simDevice.CreateJoinRequest();
            using var joinRequest = CreateWaitableRequest(joinPayload);
            messageDispatcher.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded);

            Assert.NotEmpty(savedAppSKey);
            Assert.NotEmpty(savedNwkSKey);
            Assert.NotEmpty(savedDevAddr);
            if (shouldSaveRegion)
                Assert.Equal(LoRaRegionType.EU868.ToString(), actualTwinCollection[TwinProperty.Region].Value as string);
            else
                Assert.False(actualTwinCollection.Contains(TwinProperty.Region));

            // Only save preferred gateway if device does not have one assigned
            if (shouldSavePreferredGateway)
                Assert.Equal(ServerConfiguration.GatewayID, actualTwinCollection[TwinProperty.PreferredGatewayID].Value as string);
            else
                Assert.False(actualTwinCollection.Contains(TwinProperty.PreferredGatewayID));

            Assert.True(DeviceCache.TryGetByDevEui(simDevice.DevEUI, out var loRaDevice));

            Assert.Equal(LoRaDeviceClassType.C, loRaDevice.ClassType);
            if (string.IsNullOrEmpty(simDevice.LoRaDevice.GatewayID))
                Assert.Equal(ServerConfiguration.GatewayID, loRaDevice.PreferredGatewayID);
            else
                Assert.Empty(loRaDevice.PreferredGatewayID);

            Assert.Equal(LoRaRegionType.EU868, loRaDevice.LoRaRegion);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task When_Processing_Data_Request_Should_Compute_Preferred_Gateway_And_Region(
            [CombinatorialValues(null, ServerGatewayID)] string deviceGatewayID,
            [CombinatorialValues(null, ServerGatewayID, "another-gateway")] string initialPreferredGatewayID,
            [CombinatorialValues(ServerGatewayID, "another-gateway")] string preferredGatewayID,
            [CombinatorialValues(null, LoRaRegionType.EU868, LoRaRegionType.US915)] LoRaRegionType? initialLoRaRegion)
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID, deviceClassType: LoRaDeviceClassType.C),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);
            loraDevice.UpdatePreferredGatewayID(initialPreferredGatewayID, acceptChanges: true);
            if (initialLoRaRegion.HasValue)
                loraDevice.UpdateRegion(initialLoRaRegion.Value, acceptChanges: true);

            var shouldSavePreferredGateway = string.IsNullOrEmpty(deviceGatewayID) && initialPreferredGatewayID != preferredGatewayID && preferredGatewayID == ServerGatewayID;
            var shouldSaveRegion = (!initialLoRaRegion.HasValue || initialLoRaRegion.Value != LoRaRegionType.EU868) && (preferredGatewayID == ServerGatewayID || deviceGatewayID != null);

            var bundlerResult = new FunctionBundlerResult()
            {
                PreferredGatewayResult = new PreferredGatewayResult()
                {
                    PreferredGatewayID = preferredGatewayID,
                    CurrentFcntUp = PayloadFcnt,
                    RequestFcntUp = PayloadFcnt,
                }
            };

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x =>
                    x.ExecuteFunctionBundlerAsync(simulatedDevice.DevEUI,
                                                  It.Is((FunctionBundlerRequest r) => r.ClientFCntUp == PayloadFcnt
                                                                                   && r.GatewayId == ServerGatewayID
                                                                                   && r.FunctionItems == FunctionBundlerItemType.PreferredGateway)))
                    .ReturnsAsync(bundlerResult);
            }

            TwinCollection actualSavedTwin = null;
            if (shouldSavePreferredGateway || shouldSaveRegion)
            {
                LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true)
                    .Callback((TwinCollection t, CancellationToken _) => actualSavedTwin = t);
            }

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload, constantElapsedTime: TimeSpan.Zero);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. No downstream message for the current device is sent
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            if (!string.IsNullOrEmpty(deviceGatewayID))
                Assert.Equal(initialPreferredGatewayID, loraDevice.PreferredGatewayID);
            else
                Assert.Equal(preferredGatewayID, loraDevice.PreferredGatewayID);

            Assert.Equal(LoRaRegionType.EU868, loraDevice.LoRaRegion);

            if (shouldSavePreferredGateway || shouldSaveRegion)
            {
                if (shouldSavePreferredGateway)
                    Assert.Equal(ServerGatewayID, actualSavedTwin[TwinProperty.PreferredGatewayID].Value as string);
                else
                    Assert.False(actualSavedTwin.Contains(TwinProperty.PreferredGatewayID));

                if (shouldSaveRegion)
                    Assert.Equal(LoRaRegionType.EU868.ToString(), actualSavedTwin[TwinProperty.Region].Value as string);
                else
                    Assert.False(actualSavedTwin.Contains(TwinProperty.Region));
            }
        }

        [Fact]
        public async Task When_Updating_PreferredGateway_And_FcntUp_Should_Save_Twin_Once()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, deviceClassType: LoRaDeviceClassType.C),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);
            loraDevice.UpdatePreferredGatewayID("another-gateway", acceptChanges: true);

            var bundlerResult = new FunctionBundlerResult()
            {
                PreferredGatewayResult = new PreferredGatewayResult()
                {
                    PreferredGatewayID = ServerGatewayID,
                    CurrentFcntUp = PayloadFcnt,
                    RequestFcntUp = PayloadFcnt,
                }
            };

            LoRaDeviceApi
                .Setup(x => x.ExecuteFunctionBundlerAsync(simulatedDevice.DevEUI, It.Is((FunctionBundlerRequest r) => PayloadFcnt == r.ClientFCntUp
                                                                                                                   && ServerGatewayID == r.GatewayId
                                                                                                                   && FunctionBundlerItemType.PreferredGateway == r.FunctionItems)))
                .ReturnsAsync(bundlerResult);

            TwinCollection actualSavedTwin = null;
            LoRaDeviceClient
                .Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Callback<TwinCollection, CancellationToken>((savedTwin, _) => actualSavedTwin = savedTwin)
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. No downstream message for the current device is sent
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            Assert.Equal(ServerGatewayID, loraDevice.PreferredGatewayID);
            Assert.Equal(LoRaRegionType.EU868, loraDevice.LoRaRegion);
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Once());

            Assert.Equal(ServerGatewayID, actualSavedTwin[TwinProperty.PreferredGatewayID].Value as string);
            Assert.Equal(LoRaRegionType.EU868.ToString(), actualSavedTwin[TwinProperty.Region].Value as string);
            Assert.Equal(PayloadFcnt, (uint)actualSavedTwin[TwinProperty.FCntUp].Value);
        }
    }
}
