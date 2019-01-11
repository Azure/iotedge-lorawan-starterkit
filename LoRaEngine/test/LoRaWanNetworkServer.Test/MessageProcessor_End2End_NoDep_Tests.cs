using LoRaTools.LoRaMessage;
using LoRaTools.Regions;
using LoRaTools.Utils;
using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{
    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    public class MessageProcessor_End2End_NoDep_Tests : MessageProcessorTestBase
    {
       
        public MessageProcessor_End2End_NoDep_Tests()
        {
          
        }


        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task Join_And_Send_Unconfirmed_And_Confirmed_Messages(string deviceGatewayID)
        {         
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRxpk = CreateRxpk(joinRequest);

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

            // message will be sent
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Returns(Task.FromResult(0));

            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchDevicesAsync(ServerConfiguration.GatewayID, null, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));            


            // multi gateway will request a next frame count down from the lora device api, prepare it
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                loRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, 0, 1, ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)1);
            }

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);
            

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
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
            if (deviceGatewayID == null)
                Assert.Null(loRaDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, loRaDevice.GatewayID);
            
            // fcnt is restarted
            Assert.Equal(0, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);


            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("100", fcnt: 1);
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(CreateRxpk(unconfirmedMessagePayload));
            Assert.Null(unconfirmedMessageResult);

            // fcnt up was updated
            Assert.Equal(1, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown);

            // Frame change flag will be set, only saving every 10 messages
            Assert.True(loRaDevice.HasFrameCountChanges);


            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: 2);
            var confirmedMessageRxpk = CreateRxpk(confirmedMessagePayload);
            var confirmedMessage = await messageProcessor.ProcessMessageAsync(confirmedMessageRxpk);
            Assert.NotNull(confirmedMessage);
            Assert.NotNull(confirmedMessage.txpk);
           

            // validates txpk according to eu region
            Assert.Equal(RegionFactory.CreateEU868Region().GetDownstreamChannel(confirmedMessageRxpk), confirmedMessage.txpk.freq);
            Assert.Equal("4/5", confirmedMessage.txpk.codr);
            Assert.False(confirmedMessage.txpk.imme);
            Assert.True(confirmedMessage.txpk.ipol);
            Assert.Equal("LORA", confirmedMessage.txpk.modu);

            // fcnt up was updated
            Assert.Equal(2, loRaDevice.FCntUp);
            Assert.Equal(1, loRaDevice.FCntDown);

            // Frame change flag will be set, only saving every 10 messages
            Assert.True(loRaDevice.HasFrameCountChanges);
        }




        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, "1234", "")]
        [InlineData(null, "hello world", null)]
        public async Task Unconfirmed_With_No_Decoder_Sends_Raw_Payload(string deviceGatewayID, string msgPayload, string sensorDecoder)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));            
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SensorDecoder = sensorDecoder;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .Returns(Task.FromResult(0));

            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            
            
            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );            

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = CreateRxpk(unconfirmedMessagePayload);
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);
            
            Assert.NotNull(loRaDeviceTelemetry);
            //Assert.Equal(msgPayload, loRaDeviceTelemetry.data);
        }


        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Join_Fails_Due_To_Timeout_Second_Try_Should_Reuse_Cached_Device_Twin(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest1 = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk1 = CreateRxpk(joinRequest1);

            var joinRequestDevNonce1 = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest1.DevNonce);
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
                .Callback(() =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(7));
                })
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
            loRaDeviceApi.Setup(x => x.SearchDevicesAsync(ServerConfiguration.GatewayID, null, devEUI, appEUI, joinRequestDevNonce1))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);
            
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
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
            var joinRequestRxpk2 = CreateRxpk(joinRequest2);
            var joinRequestDevNonce2 = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest2.DevNonce);
            var joinRequestDownlinkMessage2 = await messageProcessor.ProcessMessageAsync(joinRequestRxpk2);
            Assert.NotNull(joinRequestDownlinkMessage2);
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(joinRequestDownlinkMessage2.txpk.data), simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));

            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoinDevAddr);
            Assert.Single(devicesForDevAddr); // should have the single device
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));

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
        }
    }
}
