// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Only join tests
    public class MessageProcessor_End2End_NoDep_Join_Tests : MessageProcessorTestBase
    {
        public MessageProcessor_End2End_NoDep_Join_Tests()
        {
        }

        [Theory]
        [InlineData(ServerGatewayID, 200, 50, 0, 0)]
        [InlineData(ServerGatewayID, 200, 50, 17, 1)]
        [InlineData(ServerGatewayID, 0, 0, 0, 255)]
        [InlineData(ServerGatewayID, 0, 0, 27, 125)]
        [InlineData(null, 200, 50, 0, 1000)]
        [InlineData(null, 200, 50, 37, 28)]
        [InlineData(null, 0, 0, 0, 23)]
        [InlineData(null, 0, 0, 47, 10000)]
        public async Task Join_And_Send_Unconfirmed_And_Confirmed_Messages(string deviceGatewayID, uint initialFcntUp, uint initialFcntDown, uint startingPayloadFcnt, uint netId)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRxpk = joinRequestPayload.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequestPayload.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            this.ServerConfiguration.NetId = netId;
            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Reported[TwinProperty.FCntUp] = initialFcntUp;
            twin.Properties.Reported[TwinProperty.FCntDown] = initialFcntDown;
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            uint afterJoinFcntDown = 0;
            uint afterJoinFcntUp = 0;
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((updatedTwin) =>
                {
                    afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey];
                    afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey];
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                    afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown];
                    afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp];

                    // should not save class C device properties
                    Assert.False(updatedTwin.Contains(TwinProperty.Region));
                    Assert.False(updatedTwin.Contains(TwinProperty.PreferredGatewayID));
                })
                .ReturnsAsync(true);

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // multi gateway will request a next frame count down from the lora device api, prepare it
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, 0, startingPayloadFcnt + 1, this.ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)1);
                this.LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()))
                    .ReturnsAsync(() => new FunctionBundlerResult
                    {
                        AdrResult = new LoRaTools.ADR.LoRaADRResult
                        {
                            CanConfirmToDevice = false,
                            FCntDown = 1,
                            NbRepetition = 1,
                            TxPower = 0
                        },
                        NextFCntDown = 1
                    });
            }

            // using factory to create mock of
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var joinRequest = this.CreateWaitableRequest(joinRxpk, constantElapsedTime: TimeSpan.FromMilliseconds(300));
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded);
            Assert.NotNull(joinRequest.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkJoinAcceptMessage = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkJoinAcceptMessage.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));

            // check that the device is in cache
            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoinDevAddr);
            Assert.Single(devicesForDevAddr); // should have the single device
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(afterJoinAppSKey, loRaDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, loRaDevice.DevAddr);
            var netIdBytes = BitConverter.GetBytes(netId);
            Assert.Equal((uint)(netIdBytes[0] & 0b01111111), NetIdHelper.GetNwkIdPart(afterJoinDevAddr));
            if (deviceGatewayID == null)
                Assert.Null(loRaDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, loRaDevice.GatewayID);

            // fcnt is restarted
            Assert.Equal(0U, afterJoinFcntDown);
            Assert.Equal(0U, afterJoinFcntUp);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);

            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("100", fcnt: startingPayloadFcnt);
            var unconfirmedRequest = this.CreateWaitableRequest(unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0], constantElapsedTime: TimeSpan.FromMilliseconds(300));
            messageProcessor.DispatchRequest(unconfirmedRequest);
            Assert.True(await unconfirmedRequest.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest.ResponseDownlink);
            Assert.True(unconfirmedRequest.ProcessingSucceeded);

            // fcnt up was updated
            Assert.Equal(startingPayloadFcnt, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);

            if (startingPayloadFcnt != 0)
            {
                // Frame change flag will be set, only saving every 10 messages
                Assert.True(loRaDevice.HasFrameCountChanges);
            }

            Assert.Single(sentTelemetry);

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            var confirmedMessageRxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var confirmedRequest = this.CreateWaitableRequest(confirmedMessageRxpk, constantElapsedTime: TimeSpan.FromMilliseconds(300));
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.NotNull(confirmedRequest.ResponseDownlink.Txpk);
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            Assert.Equal(2, sentTelemetry.Count);
            var downstreamMessage = this.PacketForwarder.DownlinkMessages[1];

            // validates txpk according to eu region
            Assert.True(RegionManager.EU868.TryGetUpstreamChannelFrequency(confirmedMessageRxpk, out double frequency));
            Assert.Equal(frequency, downstreamMessage.Txpk.Freq);
            Assert.Equal("4/5", downstreamMessage.Txpk.Codr);
            Assert.False(downstreamMessage.Txpk.Imme);
            Assert.True(downstreamMessage.Txpk.Ipol);
            Assert.Equal("LORA", downstreamMessage.Txpk.Modu);

            // fcnt up was updated
            Assert.Equal(startingPayloadFcnt + 1, loRaDevice.FCntUp);
            Assert.Equal(1U, loRaDevice.FCntDown);

            // Frame change flag will be set, only saving every 10 messages
            Assert.True(loRaDevice.HasFrameCountChanges);

            // C2D message will be checked twice
            this.LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()), Times.Exactly(2));

            // has telemetry with both fcnt
            Assert.Single(sentTelemetry, (t) => t.Fcnt == startingPayloadFcnt);
            Assert.Single(sentTelemetry, (t) => t.Fcnt == (startingPayloadFcnt + 1));

            this.LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Join_Fails_Due_To_GetTwin_Error_Second_Try_Should_Reload_Device_Twin(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload1 = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk1 = joinRequestPayload1.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var joinRequestDevNonce1 = ConversionHelper.ByteArrayToString(joinRequestPayload1.DevNonce);
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
            this.LoRaDeviceClient.SetupSequence(x => x.GetTwinAsync())
                .ReturnsAsync((Twin)null)
                .ReturnsAsync(twin);

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
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce1))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // 1st join request
            // Should fail
            var joinRequest1 = this.CreateWaitableRequest(joinRequestRxpk1);
            messageProcessor.DispatchRequest(joinRequest1);
            Assert.True(await joinRequest1.WaitCompleteAsync());
            Assert.True(joinRequest1.ProcessingFailed);
            Assert.Null(joinRequest1.ResponseDownlink);

            // 2nd attempt
            var joinRequestPayload2 = simulatedDevice.CreateJoinRequest();

            // will reload the device matched by deveui
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, ConversionHelper.ByteArrayToString(joinRequestPayload2.DevNonce)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var joinRequestRxpk2 = joinRequestPayload2.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];
            var joinRequestDevNonce2 = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequestPayload2.DevNonce);
            var joinRequest2 = this.CreateWaitableRequest(joinRequestRxpk2);
            messageProcessor.DispatchRequest(joinRequest2);
            Assert.True(await joinRequest2.WaitCompleteAsync());
            Assert.NotNull(joinRequest2.ResponseDownlink);
            Assert.True(joinRequest2.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var joinRequestDownlinkMessage2 = this.PacketForwarder.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(joinRequestDownlinkMessage2.Txpk.Data), simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));

            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoinDevAddr);
            Assert.Single(devicesForDevAddr); // should have the single device
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));

            Assert.Equal(simulatedDevice.AppKey, loRaDevice.AppKey);
            Assert.Equal(simulatedDevice.AppEUI, loRaDevice.AppEUI);
            Assert.Equal(afterJoinAppSKey, loRaDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, loRaDevice.DevAddr);
            if (deviceGatewayID == null)
                Assert.Null(loRaDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, loRaDevice.GatewayID);

            // fcnt is restarted
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // should get twin 2x (1st failed)
            this.LoRaDeviceClient.Verify(x => x.GetTwinAsync(), Times.Exactly(2));

            // should get device for join 2x
            this.LoRaDeviceApi.Verify(x => x.SearchAndLockForJoinAsync(ServerGatewayID, devEUI, appEUI, It.IsAny<string>()));

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task Join_Device_Has_Mismatching_AppEUI_Should_Return_Null(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk = joinRequestPayload.SerializeUplink(simulatedDevice.AppKey).Rxpk[0];

            var joinRequestDevNonce = ConversionHelper.ByteArrayToString(joinRequestPayload.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = "012345678901234567890123456789FF";
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // join request should fail
            var joinRequest = this.CreateWaitableRequest(joinRequestRxpk);
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.Null(joinRequest.ResponseDownlink);

            this.LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task Join_Device_Has_Mismatching_AppKey_Should_Return_Null(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk = joinRequestPayload.SerializeUplink(simulatedDevice.AppKey).Rxpk[0];

            var joinRequestDevNonce = ConversionHelper.ByteArrayToString(joinRequestPayload.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = "012345678901234567890123456789FF";
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // join request should fail
            var joinRequest = this.CreateWaitableRequest(joinRequestRxpk);
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingFailed);
            Assert.Null(joinRequest.ResponseDownlink);

            this.LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// Downlink should use same rfch than uplink message
        /// RFCH stands for Concentrator "RF chain" used for RX
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID, 1)]
        [InlineData(ServerGatewayID, 0)]
        [InlineData(null, 1)]
        [InlineData(null, 0)]
        public async Task OTAA_Join_Should_Use_Rchf_0(string deviceGatewayID, uint rfch)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRxpk = joinRequestPayload.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];
            joinRxpk.Rfch = rfch;

            var devNonce = ConversionHelper.ByteArrayToString(joinRequestPayload.DevNonce);
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
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var joinRequest = this.CreateWaitableRequest(joinRxpk);
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded);
            Assert.NotNull(joinRequest.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkJoinAcceptMessage = this.PacketForwarder.DownlinkMessages[0];
            // validates txpk according to eu region
            Assert.Equal(0U, downlinkJoinAcceptMessage.Txpk.Rfch);
            Assert.True(RegionManager.EU868.TryGetUpstreamChannelFrequency(joinRxpk, out double receivedFrequency));
            Assert.Equal(receivedFrequency, downlinkJoinAcceptMessage.Txpk.Freq);
            Assert.Equal("4/5", downlinkJoinAcceptMessage.Txpk.Codr);
            Assert.False(downlinkJoinAcceptMessage.Txpk.Imme);
            Assert.True(downlinkJoinAcceptMessage.Txpk.Ipol);
            Assert.Equal("LORA", downlinkJoinAcceptMessage.Txpk.Modu);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Multiple_Joins_Are_Received_Should_Get_Twins_Once(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));

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
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            // Device twin will be updated
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, It.IsAny<string>()))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // 1st join request
            var joinRequest1 = this.CreateWaitableRequest(simulatedDevice.CreateJoinRequest().SerializeUplink(simulatedDevice.AppKey).Rxpk[0]);
            messageProcessor.DispatchRequest(joinRequest1);

            await Task.Delay(100);

            // 2nd join request
            var joinRequest2 = this.CreateWaitableRequest(simulatedDevice.CreateJoinRequest().SerializeUplink(simulatedDevice.AppKey).Rxpk[0]);
            messageProcessor.DispatchRequest(joinRequest2);

            await Task.WhenAll(joinRequest1.WaitCompleteAsync(), joinRequest2.WaitCompleteAsync());

            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            Assert.NotNull(joinRequest1.ResponseDownlink);
            Assert.NotNull(joinRequest2.ResponseDownlink);

            // get twin only once called
            this.LoRaDeviceClient.Verify(x => x.GetTwinAsync(), Times.Once());

            // get device for join called x2
            this.LoRaDeviceApi.Verify(x => x.SearchAndLockForJoinAsync(ServerGatewayID, devEUI, appEUI, It.IsAny<string>()), Times.Exactly(2));

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }
    }
}
