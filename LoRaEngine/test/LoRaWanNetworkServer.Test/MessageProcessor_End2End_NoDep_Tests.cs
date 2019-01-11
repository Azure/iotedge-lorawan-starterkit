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

            var downlinkMessage = await messageProcessor.ProcessMessageAsync(joinRxpk);
            Assert.NotNull(downlinkMessage);
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.txpk.data), simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));            

            // check that the device is in cache
            Assert.True(memoryCache.TryGetValue<LoRaDeviceRegistry.DevEUIDeviceDictionary>(afterJoinDevAddr, out var cachedDevices));
            Assert.True(cachedDevices.TryGetValue(devEUI, out var cachedDevice));
            Assert.Equal(afterJoinAppSKey, cachedDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, cachedDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, cachedDevice.DevAddr);
            if (deviceGatewayID == null)
                Assert.Null(cachedDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, cachedDevice.GatewayID);
            
            // fcnt is restarted
            Assert.Equal(0, cachedDevice.FCntUp);
            Assert.Equal(0, cachedDevice.FCntDown);
            Assert.False(cachedDevice.HasFrameCountChanges);


            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = afterJoinDevAddr;

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("100", fcnt: 1);
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(CreateRxpk(unconfirmedMessagePayload));
            Assert.Null(unconfirmedMessageResult);

            // fcnt up was updated
            Assert.Equal(1, cachedDevice.FCntUp);
            Assert.Equal(0, cachedDevice.FCntDown);

            // Frame change flag will be set, only saving every 10 messages
            Assert.True(cachedDevice.HasFrameCountChanges);


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
            Assert.Equal(2, cachedDevice.FCntUp);
            Assert.Equal(1, cachedDevice.FCntDown);

            // Frame change flag will be set, only saving every 10 messages
            Assert.True(cachedDevice.HasFrameCountChanges);
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
            var dictionary = new LoRaDeviceRegistry.DevEUIDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<LoRaDeviceRegistry.DevEUIDeviceDictionary>(loRaDevice.DevAddr, dictionary);

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
    }
}
