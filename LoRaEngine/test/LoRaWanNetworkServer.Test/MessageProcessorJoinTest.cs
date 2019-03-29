// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class MessageProcessorJoinTest : MessageProcessorTestBase
    {
        public MessageProcessorJoinTest()
        {
        }

        [Fact]
        public async Task When_Device_Is_Not_Found_In_Api_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => null);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            loRaDeviceRegistryMock.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Found_In_Api_Should_Update_Twin_And_Return()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            simulatedDevice.LoRaDevice.NwkSKey = string.Empty;
            simulatedDevice.LoRaDevice.AppSKey = string.Empty;
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(It.IsNotNull<string>(), devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            // Ensure that the device twin was updated
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.Is<TwinCollection>((t) =>
                t.Contains(TwinProperty.DevAddr) && t.Contains(TwinProperty.FCntDown))))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateOTAATwin());

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);

            var pktFwdMessage = this.PacketForwarder.DownlinkMessages[0];
            Assert.NotNull(pktFwdMessage.Txpk);
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(pktFwdMessage.Txpk.Data), simulatedDevice.AppKey);

            var joinedDeviceDevAddr = ConversionHelper.ByteArrayToString(joinAccept.DevAddr);
            var cachedDevices = deviceRegistry.InternalGetCachedDevicesForDevAddr(joinedDeviceDevAddr);
            Assert.Single(cachedDevices);
            Assert.True(cachedDevices.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(joinAccept.DevAddr.ToArray(), this.ByteArray(loRaDevice.DevAddr).ToArray());

            // Device properties were set with the computes values of the join operation
            Assert.Equal(joinAccept.AppNonce.ToArray(), this.ReversedByteArray(loRaDevice.AppNonce).ToArray());
            Assert.NotEmpty(loRaDevice.NwkSKey);
            Assert.NotEmpty(loRaDevice.AppSKey);
            Assert.True(loRaDevice.IsOurDevice);

            // Device frame counts were reset
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // Twin property were updated
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        private Memory<byte> ReversedByteArray(string value)
        {
            var array = ConversionHelper.StringToByteArray(value);

            Array.Reverse(array);
            return array;
        }

        Memory<byte> ByteArray(string value) => ConversionHelper.StringToByteArray(value);

        [Fact]
        public async Task When_Api_Takes_Too_Long_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .Returns(Task.Delay(TimeSpan.FromSeconds(7)).ContinueWith((_) => loRaDevice));

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.Empty(this.PacketForwarder.DownlinkMessages);
            Assert.Equal(LoRaDeviceRequestFailedReason.ReceiveWindowMissed, request.ProcessingFailedReason);

            // Device frame counts were not modified
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);

            // Twin property were updated
            this.LoRaDeviceClient.VerifyAll();
            loRaDeviceRegistryMock.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task When_Mic_Check_Fails_Join_Process_Should_Fail()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var wrongAppKey = "00000000030000000000000000030000";
            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(wrongAppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Device frame counts were not modified
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);

            // Twin property were updated
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            loRaDeviceRegistryMock.VerifyAll();
        }

        [Fact]
        public async Task When_Device_AppEUI_Does_Not_Match_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            simulatedDevice.LoRaDevice.AppEUI = "FFFFFFFFFFFFFFFF";

            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, null);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Device frame counts did not changed
            Assert.Equal(10U, loRaDevice.FCntDown);
            Assert.Equal(20U, loRaDevice.FCntUp);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            loRaDeviceRegistryMock.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Has_Different_Gateway_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: "another-gateway"));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            var loRaDeviceRegistryMock = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistryMock.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
            loRaDeviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistryMock.Object,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
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

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((updatedTwin) =>
                {
                    afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey];
                    afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey];
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var loRaDeviceFactory = new TestLoRaDeviceFactory(this.LoRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));

            // check that the device is in cache
            Assert.True(memoryCache.TryGetValue<DevEUIToLoRaDeviceDictionary>(afterJoinDevAddr, out var cachedDevices));
            Assert.True(cachedDevices.TryGetValue(devEUI, out var cachedDevice));
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
            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult() { IsDevNonceAlreadyUsed = true });

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 4)]
        [InlineData(2, 0)]
        public async Task When_Getting_DLSettings_From_Twin_Returns_JoinAccept_With_Correct_Settings(int rx1DROffset, int rx2datarate)
        {
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Desired[TwinProperty.RX1DROffset] = rx1DROffset;
            twin.Properties.Desired[TwinProperty.RX2DataRate] = rx2datarate;

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var loRaDeviceFactory = new TestLoRaDeviceFactory(this.LoRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
            Assert.Equal(rx1DROffset, joinAccept.Rx1DrOffset);
            Assert.Equal(rx2datarate, joinAccept.Rx2Dr);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(12)]
        [InlineData(-2)]
        public async Task When_Getting_Custom_RX2_DR_From_Twin_Returns_JoinAccept_With_Correct_Settings_And_Behaves_Correctly(int rx2datarate)
        {
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            int afterJoinFcntDown = -1;
            int afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
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

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
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
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var loRaDeviceFactory = new TestLoRaDeviceFactory(this.LoRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
            if (rx2datarate > 0 && rx2datarate < 8)
            {
                Assert.Equal(rx2datarate, joinAccept.Rx2Dr);
            }
            else
            {
                Assert.Equal(0, joinAccept.Rx2Dr);
            }

            // Send a message
            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            var confirmedMessageRxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var confirmedRequest = this.CreateWaitableRequest(confirmedMessageRxpk);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.NotNull(confirmedRequest.ResponseDownlink.Txpk);
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            var downstreamMessage = this.PacketForwarder.DownlinkMessages[1];

            // Message was sent on RX2 and with a correct datarate
            Assert.Equal(2000000, downstreamMessage.Txpk.Tmst - confirmedMessageRxpk.Tmst);
            if (rx2datarate > 0 && rx2datarate < 8)
            {
                Assert.Equal(rx2datarate, confirmedRequest.Region.GetDRFromFreqAndChan(downstreamMessage.Txpk.Datr));
            }
            else
            {
                Assert.Equal(0, confirmedRequest.Region.GetDRFromFreqAndChan(downstreamMessage.Txpk.Datr));
            }
        }

        [Fact]
        public async Task When_Join_With_Custom_Join_Update_Old_Desired_Properties()
        {
            var beforeJoinValues = 2;
            var afterJoinValues = 3;
            int reportedBeforeJoinRx1DROffsetValue = 0;
            int reportedBeforeJoinRx2DRValue = 0;
            int reportedBeforeJoinRxDelayValue = 0;
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            int afterJoinFcntDown = -1;
            int afterJoinFcntUp = -1;
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
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
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
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
            await this.LoRaDeviceClient.Object.UpdateReportedPropertiesAsync(startingTwin);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);
            var loRaDeviceFactory = new TestLoRaDeviceFactory(this.LoRaDeviceClient.Object);
            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);
            var joinRequest = simulatedDevice.CreateJoinRequest();
            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];
            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            twin.Properties.Desired[TwinProperty.RX2DataRate] = 3;
            await Task.Delay(TimeSpan.FromMilliseconds(10));

            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
            Assert.Equal(afterJoinValues, joinAccept.Rx2Dr);
            Assert.Equal(afterJoinValues, joinAccept.Rx1DrOffset);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRx1DROffsetValue);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRx2DRValue);
            Assert.Equal(afterJoinValues, (int)joinAccept.RxDelay.Span[0]);
            Assert.Equal(beforeJoinValues, reportedBeforeJoinRxDelayValue);
        }

        [Theory]
        // Base dr is 2
        [InlineData(0, 2)]
        [InlineData(1, 1)]
        [InlineData(2, 0)]
        [InlineData(3, 0)]
        [InlineData(4, 0)]
        [InlineData(6, 0)]
        [InlineData(12, 0)]
        [InlineData(-2, 0)]
        public async Task When_Getting_RX1_Offset_From_Twin_Returns_JoinAccept_With_Correct_Settings_And_Behaves_Correctly(int rx1offset, int expectedDR)
        {
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            int afterJoinFcntDown = -1;
            int afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
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

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
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
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var loRaDeviceFactory = new TestLoRaDeviceFactory(this.LoRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            joinAccept.DlSettings.Span.Reverse();
            if (rx1offset > 0 && rx1offset < 6)
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
            var confirmedMessageRxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var confirmedRequest = this.CreateWaitableRequest(confirmedMessageRxpk);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.NotNull(confirmedRequest.ResponseDownlink.Txpk);
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            var downstreamMessage = this.PacketForwarder.DownlinkMessages[1];

            // Message was sent on RX1 and with a correct datarate offset
            Assert.Equal(1000000, downstreamMessage.Txpk.Tmst - confirmedMessageRxpk.Tmst);
            if (rx1offset > 0 && rx1offset < 5)
            {
                Assert.Equal(expectedDR, confirmedRequest.Region.GetDRFromFreqAndChan(downstreamMessage.Txpk.Datr));
            }
            else
            {
                Assert.Equal(confirmedRequest.Region.GetDRFromFreqAndChan(confirmedMessageRxpk.Datr), confirmedRequest.Region.GetDRFromFreqAndChan(downstreamMessage.Txpk.Datr));
            }
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
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            int afterJoinFcntDown = -1;
            int afterJoinFcntUp = -1;
            uint startingPayloadFcnt = 0;

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
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

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
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
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var loRaDeviceFactory = new TestLoRaDeviceFactory(this.LoRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            joinAccept.RxDelay.Span.Reverse();
            if (rxDelay > 0 && rxDelay < 16)
            {
                Assert.Equal((int)expectedDelay, (int)joinAccept.RxDelay.Span[0]);
            }
            else
            {
                Assert.Equal(0, (int)joinAccept.RxDelay.Span[0]);
            }

            // Send a message
            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            var confirmedMessageRxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var confirmedRequest = this.CreateWaitableRequest(confirmedMessageRxpk);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.NotNull(confirmedRequest.ResponseDownlink.Txpk);
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            var downstreamMessage = this.PacketForwarder.DownlinkMessages[1];

            // Message was sent on RX1 with correct delay and with a correct datarate offset
            if (rxDelay > 0 && rxDelay < 16)
            {
                Assert.Equal(expectedDelay * 1000000, downstreamMessage.Txpk.Tmst - confirmedMessageRxpk.Tmst);
            }
            else
            {
                Assert.Equal(expectedDelay * 1000000, downstreamMessage.Txpk.Tmst - confirmedMessageRxpk.Tmst);
            }
        }
    }
}