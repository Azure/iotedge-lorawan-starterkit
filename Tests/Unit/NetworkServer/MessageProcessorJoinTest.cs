// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.RxDelay;

    public class MessageProcessorJoinTest : MessageProcessorTestBase
    {
        public MessageProcessorJoinTest(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Fact]
        public async Task When_Device_Is_Not_Found_In_Api_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEui, joinRequest.DevNonce))
                .ReturnsAsync(() => null);

            // Send to message processor
            using var cache = NewMemoryCache();
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            loRaDeviceRegistryMock.VerifyAll();

            loRaDeviceRegistryMock.Setup(dr => dr.DisposeAsync()).Returns(ValueTask.CompletedTask);
            LoRaDeviceClient.Setup(ldc => ldc.DisposeAsync()).Returns(ValueTask.CompletedTask);
        }

        [Fact]
        public async Task When_Device_Is_Found_In_Api_Should_Update_Twin_And_Return()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            simulatedDevice.LoRaDevice.NwkSKey = null;
            simulatedDevice.LoRaDevice.AppSKey = null;

            var joinRequest = simulatedDevice.CreateJoinRequest();

            // this former join request is just to set the loradevice cache to another devnonce.
            _ = simulatedDevice.CreateJoinRequest();

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(It.IsNotNull<string>(), devEui, joinRequest.DevNonce))
                         .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(null, devEui, "123") { GatewayId = ServerConfiguration.GatewayID }.AsList()));

            // Ensure that the device twin was updated
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.Is<TwinCollection>((t) =>
                t.Contains(TwinProperty.DevAddr) && t.Contains(TwinProperty.FCntDown)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties()));

            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);

            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.AppKey.Value);

            Assert.Equal(1, DeviceCache.RegistrationCount(joinAccept.DevAddr));
            Assert.True(DeviceCache.TryGetByDevEui(devEui, out var loRaDevice));
            Assert.Equal(joinAccept.DevAddr, loRaDevice.DevAddr);

            // Device properties were set with the computes values of the join operation
            Assert.Equal(joinAccept.AppNonce, loRaDevice.AppNonce);
            Assert.NotNull(loRaDevice.NwkSKey);
            Assert.NotNull(loRaDevice.AppSKey);
            Assert.True(loRaDevice.IsOurDevice);

            // Device frame counts were reset
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // Twin property were updated
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task When_Api_Takes_Too_Long_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            await using var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Loose);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEui, joinRequest.DevNonce))
                .ReturnsAsync(loRaDevice);

            // Send to message processor
            using var cache = NewMemoryCache();
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest, constantElapsedTime: TimeSpan.FromSeconds(7));
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.Empty(DownstreamMessageSender.DownlinkMessages);
            Assert.Equal(LoRaDeviceRequestFailedReason.ReceiveWindowMissed, request.ProcessingFailedReason);

            // Device frame counts were not modified
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);

            // Twin property were updated
            LoRaDeviceClient.VerifyAll();
            loRaDeviceRegistryMock.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            LoRaDeviceClient.Setup(ldc => ldc.DisposeAsync()).Returns(ValueTask.CompletedTask);
        }

        [Fact]
        public async Task When_Mic_Check_Fails_Join_Process_Should_Fail()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var wrongAppKey = TestKeys.CreateAppKey(0x3000000, 0x30000);

            var joinRequest = simulatedDevice.CreateJoinRequest(wrongAppKey);

            await using var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEui, joinRequest.DevNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            using var cache = NewMemoryCache();
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Device frame counts were not modified
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);

            // Twin property were updated
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            loRaDeviceRegistryMock.VerifyAll();

            loRaDeviceRegistryMock.Setup(dr => dr.DisposeAsync()).Returns(ValueTask.CompletedTask);;
            LoRaDeviceClient.Setup(ldc => ldc.DisposeAsync()).Returns(ValueTask.CompletedTask);;
        }

        [Fact]
        public async Task When_Device_AppEUI_Does_Not_Match_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            simulatedDevice.LoRaDevice.AppEui = new JoinEui(0xFFFFFFFFFFFFFFFF);

            await using var connectionManager = new SingleDeviceConnectionManager(LoRaDeviceClient.Object);
            await using var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEui, joinRequest.DevNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            using var cache = NewMemoryCache();
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Device frame counts did not changed
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            loRaDeviceRegistryMock.VerifyAll();

            loRaDeviceRegistryMock.Setup(dr => dr.DisposeAsync()).Returns(ValueTask.CompletedTask);
            LoRaDeviceClient.Setup(ldc => ldc.DisposeAsync()).Returns(ValueTask.CompletedTask);
        }

        [Fact]
        public async Task When_Device_Has_Different_Gateway_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: "another-gateway"));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            await using var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.IsOurDevice = false;
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>();
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEui, joinRequest.DevNonce))
                .ReturnsAsync(loRaDevice);

            // Send to message processor
            using var cache = NewMemoryCache();
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.False(request.ProcessingSucceeded);
            Assert.Equal(LoRaDeviceRequestFailedReason.HandledByAnotherGateway, request.ProcessingFailedReason);

            // Device frame counts did not changed
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Getting_Device_Information_From_Twin_Returns_JoinAccept(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties());
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            // Device twin will be updated
            AppSessionKey? afterJoinAppSKey = null;
            NetworkSessionKey? afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Callback<TwinCollection, CancellationToken>((updatedTwin, _) =>
                {
                    afterJoinAppSKey = AppSessionKey.Parse(updatedTwin[TwinProperty.AppSKey].Value);
                    afterJoinNwkSKey = NetworkSessionKey.Parse(updatedTwin[TwinProperty.NwkSKey].Value);
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            Assert.Equal(joinAccept.DevAddr.ToString(), afterJoinDevAddr);

            // check that the device is in cache
            Assert.True(DeviceCache.HasRegistrations(joinAccept.DevAddr));
            Assert.True(DeviceCache.TryGetByDevEui(devEui, out var cachedDevice));
            Assert.Equal(afterJoinAppSKey, cachedDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, cachedDevice.NwkSKey);
            Assert.Equal(joinAccept.DevAddr, cachedDevice.DevAddr);
            Assert.True(cachedDevice.IsOurDevice);
            if (deviceGatewayID == null)
                Assert.Null(cachedDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, cachedDevice.GatewayID);

            // fcnt is restarted
            Assert.Equal(0U, cachedDevice.FCntUp);
            Assert.Equal(0U, cachedDevice.FCntDown);
            Assert.False(cachedDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_Api_Returns_DevAlreadyUsed_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: null));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult() { IsDevNonceAlreadyUsed = true });

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(1, DR1)]
        [InlineData(3, DR4)]
        [InlineData(2, DR0)]
        public async Task When_Getting_DLSettings_From_Twin_Returns_JoinAccept_With_Correct_Settings(int rx1DROffset, DataRateIndex rx2datarate)
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
            {
                Rx1DROffset = rx1DROffset,
                Rx2DataRate = rx2datarate
            });

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            Assert.Equal(rx1DROffset, joinAccept.Rx1DrOffset);
            Assert.Equal(rx2datarate, joinAccept.Rx2Dr);
        }

        [Theory]
        [InlineData(DR0)]
        [InlineData(DR1)]
        [InlineData(DR2)]
        [InlineData(DR3)]
        [InlineData(DR4)]
        [InlineData(DR5)]
        [InlineData(DR6)]
        [InlineData(DR12)]
        [InlineData((DataRateIndex)(-2))]
        public async Task When_Getting_Custom_RX2_DR_From_Twin_Returns_JoinAccept_With_Correct_Settings_And_Behaves_Correctly(DataRateIndex rx2datarate)
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            AppSessionKey? afterJoinAppSKey = null;
            NetworkSessionKey? afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
            {
                Rx2DataRate = rx2datarate,
                PreferredWindow = ReceiveWindowNumber.ReceiveWindow2
            });

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
             .Callback<TwinCollection, CancellationToken>((updatedTwin, _) =>
            {
                afterJoinAppSKey = AppSessionKey.Parse(updatedTwin[TwinProperty.AppSKey].Value);
                afterJoinNwkSKey = NetworkSessionKey.Parse(updatedTwin[TwinProperty.NwkSKey].Value);
                afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr].Value;
                afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown].Value;
                afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp].Value;
            })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            if (rx2datarate is > DR0 and < DR8)
            {
                Assert.Equal(rx2datarate, joinAccept.Rx2Dr);
            }
            else
            {
                Assert.Equal(DR0, joinAccept.Rx2Dr);
            }

            // Send a message
            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = DevAddr.Parse(afterJoinDevAddr);

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            using var confirmedRequest = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), confirmedMessagePayload);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);

            TestUtils.CheckDRAndFrequencies(request, downlinkMessage);
        }

        [Fact]
        public async Task When_Join_With_Custom_Join_Update_Old_Desired_Properties()
        {
            var beforeJoinValues = 2;
            var afterJoinValues = 3;
            var reportedBeforeJoinRx1DROffsetValue = 0;
            var reportedBeforeJoinRx2DRValue = 0;
            var reportedBeforeJoinRxDelayValue = 0;
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
            {
                Rx2DataRate = (DataRateIndex)afterJoinValues,
                Rx1DROffset = afterJoinValues,
                RxDelay = afterJoinValues
            });
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
             .Callback<TwinCollection, CancellationToken>((updatedTwin, _) =>
             {
                 if (updatedTwin.Contains(TwinProperty.AppSKey))
                 {
                     afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr].Value;
                     afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown].Value;
                     afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp].Value;
                 }
                 else
                 {
                     reportedBeforeJoinRx1DROffsetValue = updatedTwin[TwinProperty.RX1DROffset].Value;
                     reportedBeforeJoinRx2DRValue = updatedTwin[TwinProperty.RX2DataRate].Value;
                     reportedBeforeJoinRxDelayValue = updatedTwin[TwinProperty.RXDelay].Value;
                 }
             })
             .ReturnsAsync(true);

            // create a state before the join
            var startingTwin = new TwinCollection();
            startingTwin[TwinProperty.RX2DataRate] = beforeJoinValues;
            startingTwin[TwinProperty.RX1DROffset] = beforeJoinValues;
            startingTwin[TwinProperty.RXDelay] = beforeJoinValues;
            await LoRaDeviceClient.Object.UpdateReportedPropertiesAsync(startingTwin, default);

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));
            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            twin.Properties.Desired[TwinProperty.RX2DataRate] = 3;
            await Task.Delay(TimeSpan.FromMilliseconds(10));

            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            Assert.Equal((DataRateIndex)afterJoinValues, joinAccept.Rx2Dr);
            Assert.Equal(afterJoinValues, joinAccept.Rx1DrOffset);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRx1DROffsetValue);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRx2DRValue);
            Assert.Equal((RxDelay)afterJoinValues, joinAccept.RxDelay);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRxDelayValue);
        }

        [Theory]
        // Base dr is 2
        // In case the offset is invalid, we rollback to offset 0. for europe that means = to upstream
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(6)]
        [InlineData(-2)]
        public async Task When_Getting_RX1_Offset_From_Twin_Returns_JoinAccept_With_Correct_Settings_And_Behaves_Correctly(int rx1offset)
        {
            var expectedDR = DR2;
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            AppSessionKey? afterJoinAppSKey = null;
            NetworkSessionKey? afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;


            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
            {
                Rx1DROffset = rx1offset,
                PreferredWindow = ReceiveWindowNumber.ReceiveWindow1
            });

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
             .Callback<TwinCollection, CancellationToken>((updatedTwin, _) =>
             {
                 afterJoinAppSKey = AppSessionKey.Parse(updatedTwin[TwinProperty.AppSKey].Value);
                 afterJoinNwkSKey = NetworkSessionKey.Parse(updatedTwin[TwinProperty.NwkSKey].Value);
                 afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr].Value;
                 afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown].Value;
                 afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp].Value;
             })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            if (rx1offset is > 0 and < 6)
            {
                Assert.Equal(rx1offset, joinAccept.Rx1DrOffset);
            }
            else
            {
                Assert.Equal(0, joinAccept.Rx1DrOffset);
            }

            // Send a message
            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = DevAddr.Parse(afterJoinDevAddr);

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            using var confirmedRequest = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), confirmedMessagePayload);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);

            var euRegion = RegionManager.EU868;

            Assert.Equal(expectedDR, downlinkMessage.Rx1?.DataRate);
            Assert.Equal(euRegion.GetDownstreamRX2DataRate(null, null, null, NullLogger.Instance), downlinkMessage.Rx2.DataRate);

            // Ensure RX freq
            Assert.True(euRegion.TryGetDownstreamChannelFrequency(request.RadioMetadata.Frequency, request.RadioMetadata.DataRate, deviceJoinInfo: null, downstreamFrequency: out var channelFrequency));
            Assert.Equal(channelFrequency, downlinkMessage.Rx1?.Frequency);

            Assert.Equal(euRegion.GetDownstreamRX2Freq(null, null, logger: NullLogger.Instance), downlinkMessage.Rx2.Frequency);
        }

        [Theory]
        [InlineData(0, RxDelay0)]
        [InlineData(1, RxDelay1)]
        [InlineData(2, RxDelay2)]
        [InlineData(3, RxDelay3)]
        [InlineData(15, RxDelay15)]
        [InlineData(16, RxDelay1)]
        [InlineData(-2, RxDelay1)]
        [InlineData(200, RxDelay1)]
        [InlineData(2147483647, RxDelay1)]
        public async Task When_Getting_RXDelay_Offset_From_Twin_Returns_JoinAccept_With_Correct_Settings_And_Behaves_Correctly(int rxDelay, RxDelay expectedDelay)
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            AppSessionKey? afterJoinAppSKey = null;
            NetworkSessionKey? afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
            {
                RxDelay = rxDelay,
                PreferredWindow = ReceiveWindowNumber.ReceiveWindow1
            });

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
             .Callback<TwinCollection, CancellationToken>((updatedTwin, _) =>
             {
                 afterJoinAppSKey = AppSessionKey.Parse(updatedTwin[TwinProperty.AppSKey].Value);
                 afterJoinNwkSKey = NetworkSessionKey.Parse(updatedTwin[TwinProperty.NwkSKey].Value);
                 afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr].Value;
                 afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown].Value;
                 afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp].Value;
             })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            if (rxDelay is >= 0 and < 16)
            {
                Assert.Equal(expectedDelay, joinAccept.RxDelay);
            }
            else
            {
                Assert.Equal(RxDelay0, joinAccept.RxDelay);
            }

            // Send a message
            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = DevAddr.Parse(afterJoinDevAddr);

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            using var confirmedRequest = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), confirmedMessagePayload);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);
        }
    }
}
