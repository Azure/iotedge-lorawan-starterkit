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
    using global::LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;
    using static LoRaWan.DataRateIndex;

    public class MessageProcessorJoinTest : MessageProcessorTestBase
    {
        [Fact]
        public async Task When_Device_Is_Not_Found_In_Api_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, joinRequest.DevNonce))
                .ReturnsAsync(() => null);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            loRaDeviceRegistryMock.VerifyAll();

            loRaDeviceRegistryMock.Setup(dr => dr.Dispose());
            LoRaDeviceClient.Setup(ldc => ldc.Dispose());
        }

        [Fact]
        public async Task When_Device_Is_Found_In_Api_Should_Update_Twin_And_Return()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            simulatedDevice.LoRaDevice.NwkSKey = string.Empty;
            simulatedDevice.LoRaDevice.AppSKey = string.Empty;

            var joinRequest = simulatedDevice.CreateJoinRequest();

            // this former join request is just to set the loradevice cache to another devnonce.
            _ = simulatedDevice.CreateJoinRequest();

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;


            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(It.IsNotNull<string>(), devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123") { GatewayId = ServerConfiguration.GatewayID }.AsList()));

            // Ensure that the device twin was updated
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.Is<TwinCollection>((t) =>
                t.Contains(TwinProperty.DevAddr) && t.Contains(TwinProperty.FCntDown))))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simulatedDevice.CreateOTAATwin());

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(PacketForwarder.DownlinkMessages);

            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.AppKey);

            var joinedDeviceDevAddr = ConversionHelper.ByteArrayToString(joinAccept.DevAddr);
            Assert.Equal(1, DeviceCache.RegistrationCount(joinedDeviceDevAddr));
            Assert.True(DeviceCache.TryGetByDevEui(devEUI, out var loRaDevice));
            Assert.Equal(joinAccept.DevAddr.ToArray(), ByteArray(loRaDevice.DevAddr).ToArray());

            // Device properties were set with the computes values of the join operation
            Assert.Equal(joinAccept.AppNonce.ToArray(), ReversedByteArray(loRaDevice.AppNonce).ToArray());
            Assert.NotEmpty(loRaDevice.NwkSKey);
            Assert.NotEmpty(loRaDevice.AppSKey);
            Assert.True(loRaDevice.IsOurDevice);

            // Device frame counts were reset
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // Twin property were updated
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        private static Memory<byte> ReversedByteArray(string value)
        {
            var array = ConversionHelper.StringToByteArray(value);

            Array.Reverse(array);
            return array;
        }

        private static Memory<byte> ByteArray(string value) => ConversionHelper.StringToByteArray(value);

        [Fact]
        public async Task When_Api_Takes_Too_Long_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            using var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Loose);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, joinRequest.DevNonce))
                .ReturnsAsync(loRaDevice);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest, constantElapsedTime: TimeSpan.FromSeconds(7));
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.Empty(PacketForwarder.DownlinkMessages);
            Assert.Equal(LoRaDeviceRequestFailedReason.ReceiveWindowMissed, request.ProcessingFailedReason);

            // Device frame counts were not modified
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);

            // Twin property were updated
            LoRaDeviceClient.VerifyAll();
            loRaDeviceRegistryMock.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            LoRaDeviceClient.Setup(ldc => ldc.Dispose());
        }

        [Fact]
        public async Task When_Mic_Check_Fails_Join_Process_Should_Fail()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var wrongAppKey = "00000000030000000000000000030000";

            var joinRequest = simulatedDevice.CreateJoinRequest(wrongAppKey);

            using var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, joinRequest.DevNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
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

            loRaDeviceRegistryMock.Setup(dr => dr.Dispose());
            LoRaDeviceClient.Setup(ldc => ldc.Dispose());
        }

        [Fact]
        public async Task When_Device_AppEUI_Does_Not_Match_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            simulatedDevice.LoRaDevice.AppEUI = "FFFFFFFFFFFFFFFF";

            using var connectionManager = new SingleDeviceConnectionManager(LoRaDeviceClient.Object);
            using var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, joinRequest.DevNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
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

            loRaDeviceRegistryMock.Setup(dr => dr.Dispose());
            LoRaDeviceClient.Setup(ldc => ldc.Dispose());
        }

        [Fact]
        public async Task When_Device_Has_Different_Gateway_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: "another-gateway"));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            using var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.IsOurDevice = false;
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>();
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, joinRequest.DevNonce))
                .ReturnsAsync(loRaDevice);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
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


            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            if (deviceGatewayID != null) twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((updatedTwin) =>
                {
                    afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey];
                    afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey];
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(PacketForwarder.DownlinkMessages);
            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));

            // check that the device is in cache
            Assert.True(DeviceCache.HasRegistrations(afterJoinDevAddr));
            Assert.True(DeviceCache.TryGetByDevEui(devEUI, out var cachedDevice));
            Assert.Equal(afterJoinAppSKey, cachedDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, cachedDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, cachedDevice.DevAddr);
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

            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult() { IsDevNonceAlreadyUsed = true });

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
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

            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Desired[TwinProperty.RX1DROffset] = rx1DROffset;
            twin.Properties.Desired[TwinProperty.RX2DataRate] = rx2datarate;

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(PacketForwarder.DownlinkMessages);
            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
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
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;


            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Desired[TwinProperty.RX2DataRate] = rx2datarate;
            twin.Properties.Desired[TwinProperty.PreferredWindow] = 2;

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
             .Callback<TwinCollection>((updatedTwin) =>
            {
                afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey].Value;
                afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey].Value;
                afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr].Value;
                afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown].Value;
                afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp].Value;
            })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(PacketForwarder.DownlinkMessages);
            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
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
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            using var confirmedRequest = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), confirmedMessagePayload);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Equal(2, PacketForwarder.DownlinkMessages.Count);

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
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Desired[TwinProperty.RX2DataRate] = afterJoinValues;
            twin.Properties.Desired[TwinProperty.RX1DROffset] = afterJoinValues;
            twin.Properties.Desired[TwinProperty.RXDelay] = afterJoinValues;
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
             .Callback<TwinCollection>((updatedTwin) =>
             {
                 if (updatedTwin.Contains(TwinProperty.AppSKey))
                 {
                     afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey].Value;
                     afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey].Value;
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
            await LoRaDeviceClient.Object.UpdateReportedPropertiesAsync(startingTwin);

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));
            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            twin.Properties.Desired[TwinProperty.RX2DataRate] = 3;
            await Task.Delay(TimeSpan.FromMilliseconds(10));

            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
            Assert.Equal((DataRateIndex)afterJoinValues, joinAccept.Rx2Dr);
            Assert.Equal(afterJoinValues, joinAccept.Rx1DrOffset);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRx1DROffsetValue);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRx2DRValue);
            Assert.Equal(afterJoinValues, joinAccept.RxDelay.Span[0]);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRxDelayValue);
        }

        [Theory]
        // Base dr is 2
        // In case the offset is invalid, we rollback to offset 0. for europe that means = to upstream
        [InlineData(0, DR2)]
        [InlineData(1, DR1)]
        [InlineData(2, DR0)]
        [InlineData(3, DR0)]
        [InlineData(4, DR0)]
        [InlineData(6, DR2)]
        [InlineData(12, DR2)]
        [InlineData(-2, DR2)]
        public async Task When_Getting_RX1_Offset_From_Twin_Returns_JoinAccept_With_Correct_Settings_And_Behaves_Correctly(int rx1offset, DataRateIndex expectedDR)
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;


            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Desired[TwinProperty.RX1DROffset] = rx1offset;
            twin.Properties.Desired[TwinProperty.PreferredWindow] = 1;

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
             .Callback<TwinCollection>((updatedTwin) =>
             {
                 afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey].Value;
                 afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey].Value;
                 afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr].Value;
                 afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown].Value;
                 afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp].Value;
             })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(PacketForwarder.DownlinkMessages);
            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
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
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            using var confirmedRequest = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), confirmedMessagePayload);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Equal(2, PacketForwarder.DownlinkMessages.Count);

            var euRegion = RegionManager.EU868;

            Assert.Equal(expectedDR, downlinkMessage.DataRateRx1);
            Assert.Equal(euRegion.GetDownstreamRX2DataRate(null, null, NullLogger.Instance), downlinkMessage.DataRateRx2);

            // Ensure RX freq
            Assert.True(euRegion.TryGetDownstreamChannelFrequency(request.RadioMetadata.Frequency,out var channelFrequency));
            Assert.Equal(channelFrequency, downlinkMessage.FrequencyRx1);

            Assert.Equal(euRegion.GetDownstreamRX2Freq(null, NullLogger.Instance), downlinkMessage.FrequencyRx2);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(15, 15)]
        [InlineData(16, 1)]
        [InlineData(-2, 1)]
        [InlineData(200, 1)]
        [InlineData(2147483647, 1)]
        public async Task When_Getting_RXDelay_Offset_From_Twin_Returns_JoinAccept_With_Correct_Settings_And_Behaves_Correctly(int rxDelay, uint expectedDelay)
        {
            var deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            var afterJoinFcntDown = -1;
            var afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;

            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Desired[TwinProperty.RXDelay] = rxDelay;
            twin.Properties.Desired[TwinProperty.PreferredWindow] = 1;

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
             .Callback<TwinCollection>((updatedTwin) =>
             {
                 afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey].Value;
                 afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey].Value;
                 afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr].Value;
                 afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown].Value;
                 afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp].Value;
             })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequest.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), joinRequest);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(PacketForwarder.DownlinkMessages);
            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey);
            joinAccept.RxDelay.Span.Reverse();
            if (rxDelay is > 0 and < 16)
            {
                Assert.Equal((int)expectedDelay, joinAccept.RxDelay.Span[0]);
            }
            else
            {
                Assert.Equal(0, joinAccept.RxDelay.Span[0]);
            }

            // Send a message
            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            using var confirmedRequest = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), confirmedMessagePayload);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Equal(2, PacketForwarder.DownlinkMessages.Count);
        }
    }
}
