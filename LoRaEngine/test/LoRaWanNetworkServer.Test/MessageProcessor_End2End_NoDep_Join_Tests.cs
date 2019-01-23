//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools.LoRaMessage;
using LoRaTools.Regions;
using LoRaTools.Utils;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;


namespace LoRaWan.NetworkServer.Test
{
    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Only join tests
    public class MessageProcessor_End2End_NoDep_Join_Tests : MessageProcessorTestBase
    {
       
        public MessageProcessor_End2End_NoDep_Join_Tests()
        {
          
        }


        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, 200, 50, 0, 0)]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, 200, 50, 17, 1)]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, 0, 0, 0, 255)]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, 0, 0, 27, 125)]
        [InlineData(null, 200, 50, 0, 1000)]
        [InlineData(null, 200, 50, 37, 28)]
        [InlineData(null, 0, 0, 0, 23)]
        [InlineData(null, 0, 0, 47, 10000)]
        public async Task Join_And_Send_Unconfirmed_And_Confirmed_Messages(string deviceGatewayID, int initialFcntUp, int initialFcntDown, int startingPayloadFcnt,uint netId)
        {         
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];
            
            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            ServerConfiguration.NetId = netId;
            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            twin.Properties.Reported[TwinProperty.FCntUp] = initialFcntUp;
            twin.Properties.Reported[TwinProperty.FCntDown] = initialFcntDown;
            loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            int afterJoinFcntDown = -1;
            int afterJoinFcntUp = -1;
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((updatedTwin) => {
                    afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey];
                    afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey];
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                    afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown];
                    afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp];
                })
                .ReturnsAsync(true);

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));            


            // multi gateway will request a next frame count down from the lora device api, prepare it
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                loRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, 0, startingPayloadFcnt + 1, ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)1);
            }

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);
            

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            var downlinkJoinAcceptMessage = await messageProcessor.ProcessMessageAsync(joinRxpk);
            Assert.NotNull(downlinkJoinAcceptMessage);
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkJoinAcceptMessage.txpk.data), simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));

            // check that the device is in cache
            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoinDevAddr);
            Assert.Single(devicesForDevAddr); // should have the single device
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(afterJoinAppSKey, loRaDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, loRaDevice.DevAddr);
            var netIdBytes=BitConverter.GetBytes(netId);           
            Assert.Equal((uint)(netIdBytes[0]&0b01111111),LoRaTools.Utils.NetIdHelper.GetNwkIdPart(afterJoinDevAddr));
            if (deviceGatewayID == null)
                Assert.Null(loRaDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, loRaDevice.GatewayID);

            // fcnt is restarted
            Assert.Equal(0, afterJoinFcntDown);
            Assert.Equal(0, afterJoinFcntUp);
            Assert.Equal(0, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);


            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends unconfirmed message            
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("100", fcnt: startingPayloadFcnt);
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0]);
            Assert.Null(unconfirmedMessageResult);

            // fcnt up was updated
            Assert.Equal(startingPayloadFcnt, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown);

            if (startingPayloadFcnt != 0)
            {
                // Frame change flag will be set, only saving every 10 messages
                Assert.True(loRaDevice.HasFrameCountChanges);
            }
            Assert.Single(sentTelemetry);

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            var confirmedMessageRxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var confirmedMessage = await messageProcessor.ProcessMessageAsync(confirmedMessageRxpk);
            Assert.NotNull(confirmedMessage);
            Assert.NotNull(confirmedMessage.txpk);
            Assert.Equal(2, sentTelemetry.Count);



            // validates txpk according to eu region
            Assert.Equal(RegionFactory.CreateEU868Region().GetDownstreamChannel(confirmedMessageRxpk), confirmedMessage.txpk.freq);
            Assert.Equal("4/5", confirmedMessage.txpk.codr);
            Assert.False(confirmedMessage.txpk.imme);
            Assert.True(confirmedMessage.txpk.ipol);
            Assert.Equal("LORA", confirmedMessage.txpk.modu);

            // fcnt up was updated
            Assert.Equal(startingPayloadFcnt + 1, loRaDevice.FCntUp);
            Assert.Equal(1, loRaDevice.FCntDown);

            // Frame change flag will be set, only saving every 10 messages
            Assert.True(loRaDevice.HasFrameCountChanges);


            // C2D message will be checked twice
            loRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()), Times.Exactly(2));

            // has telemetry with both fcnt
            Assert.Single(sentTelemetry, (t) => t.Fcnt == startingPayloadFcnt);
            Assert.Single(sentTelemetry, (t) => t.Fcnt == (startingPayloadFcnt + 1));

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }



        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Join_Fails_Due_To_GetTwin_Error_Second_Try_Should_Reload_Device_Twin(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest1 = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk1 = joinRequest1.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];

            var joinRequestDevNonce1 = ConversionHelper.ByteArrayToString(joinRequest1.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            loRaDeviceClient.SetupSequence(x => x.GetTwinAsync())
                .ReturnsAsync((Twin)null)
                .ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((updatedTwin) => {
                    afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey];
                    afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey];
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce1))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // 1st join request
            // Should fail
            var joinRequestDownlinkMessage1 = await messageProcessor.ProcessMessageAsync(joinRequestRxpk1);
            Assert.Null(joinRequestDownlinkMessage1);

            // 2nd attempt
            var joinRequest2 = simulatedDevice.CreateJoinRequest();

            // will reload the device matched by deveui
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, ConversionHelper.ByteArrayToString(joinRequest2.DevNonce)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var joinRequestRxpk2 = joinRequest2.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];
            var joinRequestDevNonce2 = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest2.DevNonce);
            var joinRequestDownlinkMessage2 = await messageProcessor.ProcessMessageAsync(joinRequestRxpk2);
            Assert.NotNull(joinRequestDownlinkMessage2);
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(joinRequestDownlinkMessage2.txpk.data), simulatedDevice.LoRaDevice.AppKey);
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
            Assert.Equal(0, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // should get twin 2x (1st failed)
            loRaDeviceClient.Verify(x => x.GetTwinAsync(), Times.Exactly(2));

            // should get device for join 2x
            loRaDeviceApi.Verify(x => x.SearchAndLockForJoinAsync(ServerGatewayID, devEUI, appEUI, It.IsAny<string>()));

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }


        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task Join_Device_Has_Mismatching_AppEUI_Should_Return_Null(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk = joinRequest.SerializeUplink(simulatedDevice.AppKey).rxpk[0];

            var joinRequestDevNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = "012345678901234567890123456789FF";
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);
         

            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // join request should fail
            var joinRequestDownlinkMessage = await messageProcessor.ProcessMessageAsync(joinRequestRxpk);
            Assert.Null(joinRequestDownlinkMessage);

            loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task Join_Device_Has_Mismatching_AppKey_Should_Return_Null(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk = joinRequest.SerializeUplink(simulatedDevice.AppKey).rxpk[0];

            var joinRequestDevNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = "012345678901234567890123456789FF";
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);


            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // join request should fail
            var joinRequestDownlinkMessage = await messageProcessor.ProcessMessageAsync(joinRequestRxpk);
            Assert.Null(joinRequestDownlinkMessage);

            loRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }

       


        /// <summary>
        /// Downlink should use same rfch than uplink message
        /// RFCH stands for Concentrator "RF chain" used for RX
        /// </summary>
        /// <param name="deviceGatewayID"></param>
        /// <param name="rfch"></param>
        /// <returns></returns>
        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, 1)]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, 0)]
        [InlineData(null, 1)]
        [InlineData(null, 0)]
        public async Task OTAA_Join_Should_Use_Rchf_0(string deviceGatewayID, uint rfch)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];
            joinRxpk.rfch = rfch;

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            // Device twin will be updated
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);


            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            var downlinkJoinAcceptMessage = await messageProcessor.ProcessMessageAsync(joinRxpk);
            Assert.NotNull(downlinkJoinAcceptMessage);
            // validates txpk according to eu region
            Assert.Equal((uint)0, downlinkJoinAcceptMessage.txpk.rfch);
            Assert.Equal(RegionFactory.CreateEU868Region().GetDownstreamChannel(joinRxpk), downlinkJoinAcceptMessage.txpk.freq);
            Assert.Equal("4/5", downlinkJoinAcceptMessage.txpk.codr);
            Assert.False(downlinkJoinAcceptMessage.txpk.imme);
            Assert.True(downlinkJoinAcceptMessage.txpk.ipol);
            Assert.Equal("LORA", downlinkJoinAcceptMessage.txpk.modu);

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }


        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Multiple_Joins_Are_Received_Should_Get_Twins_Once(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            // Device twin will be updated
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, It.IsAny<string>()))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // 1st join request     
            var joinRequest1Response = messageProcessor.ProcessMessageAsync(simulatedDevice.CreateJoinRequest().SerializeUplink(simulatedDevice.AppKey).rxpk[0]);
            
            // 2nd join request     
            var joinRequest2Response = messageProcessor.ProcessMessageAsync(simulatedDevice.CreateJoinRequest().SerializeUplink(simulatedDevice.AppKey).rxpk[0]);
            
            await Task.WhenAll(joinRequest1Response, joinRequest2Response);

            Assert.NotNull(joinRequest1Response.Result);
            Assert.NotNull(joinRequest2Response.Result);

            
            // get twin only once called
            loRaDeviceClient.Verify(x => x.GetTwinAsync(), Times.Once());

            // get device for join called x2
            loRaDeviceApi.Verify(x => x.SearchAndLockForJoinAsync(ServerGatewayID, devEUI, appEUI, It.IsAny<string>()));

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }
    }
}
