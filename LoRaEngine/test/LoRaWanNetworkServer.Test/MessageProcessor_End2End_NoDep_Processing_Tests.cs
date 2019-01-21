//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaTools.Regions;
using LoRaTools.Utils;
using LoRaWan.NetworkServer;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace LoRaWan.NetworkServer.Test
{
    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // General message processor tests (Join tests are handled in other class)
    public class MessageProcessor_End2End_NoDep_Processing_Tests : MessageProcessorTestBase
    {
       
        public MessageProcessor_End2End_NoDep_Processing_Tests()
        {
          
        }

        

        [Theory]
        [InlineData(ServerGatewayID, 0, 0, 0)]
        [InlineData(ServerGatewayID, 0, 1, 1)]
        [InlineData(ServerGatewayID, 0, 100, 20)]
        [InlineData(ServerGatewayID, 1, 0, 0)]
        [InlineData(ServerGatewayID, 1, 1, 1)]
        [InlineData(ServerGatewayID, 1, 100, 20)]
        [InlineData(null, 0, 0, 0)]
        [InlineData(null, 0, 1, 1)]
        [InlineData(null, 0, 100, 20)]
        [InlineData(null, 1, 0, 0)]
        [InlineData(null, 1, 1, 1)]
        [InlineData(null, 1, 100, 20)]
        public async Task ABP_Cached_Device_With_Fcnt_1_Or_0_Should_Reset_Fcnt_And_Send_To_IotHub(
            string deviceGatewayID,
            int payloadFcntUp,
            int deviceInitialFcntUp,
            int deviceInitialFcntDown)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntDown = deviceInitialFcntDown;
            simulatedDevice.FrmCntUp = deviceInitialFcntUp;

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // twin will be updated with new fcnt
            int? fcntUpSavedInTwin = null;
            int? fcntDownSavedInTwin = null;

            // twin should be saved only if not starting at 0, 0
            var shouldSaveTwin = deviceInitialFcntDown != 0 || deviceInitialFcntUp != 0;
            if (shouldSaveTwin)
            {
                // Twin will be saved
                loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .Callback<TwinCollection>((t) =>
                    {
                        fcntUpSavedInTwin = (int)t[TwinProperty.FCntUp];
                        fcntDownSavedInTwin = (int)t[TwinProperty.FCntDown];
                    })
                    .ReturnsAsync(true);
            }


            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcntUp);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);

            // Ensure that a telemetry was sent
            Assert.NotNull(loRaDeviceTelemetry);
            //Assert.Equal(msgPayload, loRaDeviceTelemetry.data);

            // Ensure that the device twins were saved 
            if (shouldSaveTwin)
            {
                Assert.NotNull(fcntDownSavedInTwin);
                Assert.NotNull(fcntUpSavedInTwin);
                Assert.Equal(0, fcntDownSavedInTwin.Value); // fcntDown will be set to zero
                Assert.Equal(0, fcntUpSavedInTwin.Value); // fcntUp will be set to zero
            }

            // verify that the device in device registry has correct properties and frame counters
            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Single(devicesForDevAddr);
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEUI, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(payloadFcntUp, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown); // fctn down will always be set to zero
            if (payloadFcntUp == 0)
                Assert.False(loRaDevice.HasFrameCountChanges); // no changes
            else
                Assert.True(loRaDevice.HasFrameCountChanges); // there are pending changes (fcntUp 0 => 1)
            
            // will update api in multi gateway scenario
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                loRaDeviceApi.Verify(x => x.ABPFcntCacheResetAsync(devEUI), Times.Exactly(1));
            }

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(0, null, null)]
        [InlineData(0, 1, 1)]
        [InlineData(0, 100, 20)]
        [InlineData(1, null, null)]
        [InlineData(1, 1, 1)]
        [InlineData(1, 100, 20)]
        public async Task SingleGateway_ABP_New_Loaded_Device_With_Fcnt_1_Or_0_Should_Reset_Fcnt_And_Send_To_IotHub(
            int payloadFcntUp, 
            int? deviceTwinFcntUp, 
            int? deviceTwinFcntDown)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));            
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // twin will be loaded
            var initialTwin = new Twin();
            initialTwin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            initialTwin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            initialTwin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            initialTwin.Properties.Desired[TwinProperty.NwkSKey] = simulatedDevice.LoRaDevice.NwkSKey;
            initialTwin.Properties.Desired[TwinProperty.AppSKey] = simulatedDevice.LoRaDevice.AppSKey;
            initialTwin.Properties.Desired[TwinProperty.DevAddr] = devAddr;
            initialTwin.Properties.Desired[TwinProperty.GatewayID] = this.ServerConfiguration.GatewayID;            
            initialTwin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            if (deviceTwinFcntDown.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntDown] = deviceTwinFcntDown.Value;
            if (deviceTwinFcntUp.HasValue)  
                initialTwin.Properties.Reported[TwinProperty.FCntUp] = deviceTwinFcntUp.Value;
            
            loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(initialTwin);

            // twin will be updated with new fcnt
            int? fcntUpSavedInTwin = null;
            int? fcntDownSavedInTwin = null;

            // Twin will be save (0, 0) only if it was not 0, 0
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((t) =>
                {
                    fcntUpSavedInTwin = (int)t[TwinProperty.FCntUp];
                    fcntDownSavedInTwin = (int)t[TwinProperty.FCntDown];
                })
                .ReturnsAsync(true);
            
       
            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // device api will be searched for payload
            loRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));
            
            
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

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcntUp);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);

            // Ensure that a telemetry was sent
            Assert.NotNull(loRaDeviceTelemetry);
            //Assert.Equal(msgPayload, loRaDeviceTelemetry.data);

            // Ensure that the device twins were saved 
            Assert.NotNull(fcntDownSavedInTwin);
            Assert.NotNull(fcntUpSavedInTwin);
            Assert.Equal(0, fcntDownSavedInTwin.Value);
            Assert.Equal(0, fcntUpSavedInTwin.Value);

            // verify that the device in device registry has correct properties and frame counters
            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Single(devicesForDevAddr);
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEUI, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(payloadFcntUp, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown);
            if (payloadFcntUp == 0)
                Assert.False(loRaDevice.HasFrameCountChanges); // no changes
            else
                Assert.True(loRaDevice.HasFrameCountChanges); // there are pending changes (fcntUp 0 => 1)
            
            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }


        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, "1234", "")]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, "hello world", null)]
        [InlineData(null, "hello world", null)]
        [InlineData(null, "1234", "")]
        public async Task ABP_Unconfirmed_With_No_Decoder_Sends_Raw_Payload(string deviceGatewayID, string msgPayload, string sensorDecoder)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));            
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SensorDecoder = sensorDecoder;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

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
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );            

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            //Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out var parsedPayload));
            //Assert.Equal(msgPayload,  Encoding.UTF8.GetString(((LoRaPayloadData)parsedPayload).GetDecryptedPayload(simulatedDevice.AppSKey)));

            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);
            
            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<string>(loRaDeviceTelemetry.data);
            var expectedPayloadContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
            Assert.Equal(expectedPayloadContent, loRaDeviceTelemetry.data);

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }


        [Theory]
        [InlineData(0,0)]
        [InlineData(0, 1)]
        [InlineData(255, 1)]
        [InlineData(16777215, 16777215)]
        [InlineData(127, 127)]
        [InlineData(255, 255)]
        public async Task ABP_Device_NetId_Should_Match_Server(uint deviceNetId,uint serverNetId)
        {
            string msgPayload = "1234";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, netId:deviceNetId));
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SensorDecoder = null;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

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
            this.ServerConfiguration.NetId = serverNetId;
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            //Assert.True(LoRaPayload.TryCreateLoRaPayload(rxpk, out var parsedPayload));
            //Assert.Equal(msgPayload,  Encoding.UTF8.GetString(((LoRaPayloadData)parsedPayload).GetDecryptedPayload(simulatedDevice.AppSKey)));

            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);

            if (deviceNetId==serverNetId) {
                Assert.NotNull(loRaDeviceTelemetry);
                Assert.IsType<string>(loRaDeviceTelemetry.data);
                var expectedPayloadContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
                Assert.Equal(expectedPayloadContent, loRaDeviceTelemetry.data);
                loRaDeviceClient.VerifyAll();
                loRaDeviceApi.VerifyAll();

            }
            else
            {
                Assert.Null(loRaDeviceTelemetry);
                Assert.Equal(0, loRaDevice.FCntUp);
            }

        }

        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, "1234")]
        [InlineData(null, "1234")]
        public async Task ABP_Unconfirmed_With_Value_Decoder_Sends_Decoded_Numeric_Payload(string deviceGatewayID, string msgPayload)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

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
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
 
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<JObject>(loRaDeviceTelemetry.data);
            var telemetryData = (JObject)loRaDeviceTelemetry.data;
            Assert.Equal(msgPayload, telemetryData["value"].ToString());

            loRaDeviceApi.VerifyAll();
            loRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, null)]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, "test")]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, "test","idtest")]
        public async Task When_Ack_Message_Received_Should_Be_In_Msg_Properties(string deviceGatewayID,string data,string msgId=null)
        {
            const int initialFcntUp = 100;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntUp = initialFcntUp;
            var ackMessage = simulatedDevice.CreateUnconfirmedDataUpMessage(data,fctrl:(byte)FctrlEnum.Ack);
            var ackRxpk=ackMessage.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            if (msgId != null)
                loRaDevice.LastConfirmedC2DMessageID = msgId;

            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);
            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);
            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry,Dictionary<string,string>>((t, d) =>
                {

                    Assert.NotNull(d);
                    Assert.True(d.ContainsKey(MessageProcessor.C2D_MSG_PROPERTY_VALUE_NAME));

                    if (msgId == null)
                        Assert.True(d.ContainsValue(MessageProcessor.C2D_MSG_ID_PLACEHOLDER));
                    else
                        Assert.True(d.ContainsValue(msgId));
                })
                .Returns(Task.FromResult(true));
      
            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var messageProcessor = new MessageProcessor(
               this.ServerConfiguration,
               deviceRegistry,
               frameCounterUpdateStrategyFactory,
               new LoRaPayloadDecoder()
               );
            var ackTxpk = await messageProcessor.ProcessMessageAsync(ackRxpk);
            Assert.Null(ackTxpk);
            Assert.True(deviceRegistry.InternalGetCachedDevicesForDevAddr(loRaDevice.DevAddr).TryGetValue(loRaDevice.DevEUI, out var loRaDeviceInfo));
            
            Assert.Equal(loRaDeviceInfo.FCntUp, simulatedDevice.FrmCntUp+1);


        }

        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID, 21)]
        [InlineData(null, 21)]
        [InlineData(null, 30)]
        public async Task When_ConfirmedUp_Message_With_Same_Fcnt_Should_Return_Ack_And_Not_Send_To_Hub(string deviceGatewayID, int expectedFcntDown)
        {
            const int initialFcntUp = 100;
            const int initialFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntUp = initialFcntUp;
            simulatedDevice.FrmCntDown = initialFcntDown;
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // in multigateway scenario the device api will be called to resolve fcntDown
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                loRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, 20, 100, ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)expectedFcntDown);
            }

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
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("repeat", fcnt: 100);
            var rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var confirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            
            // ack should be received
            Assert.NotNull(confirmedMessageResult);
            Assert.NotNull(confirmedMessageResult.txpk);


            // validates txpk according to eu region
            Assert.Equal(RegionFactory.CreateEU868Region().GetDownstreamChannel(rxpk), confirmedMessageResult.txpk.freq);
            Assert.Equal("4/5", confirmedMessageResult.txpk.codr);
            Assert.False(confirmedMessageResult.txpk.imme);
            Assert.True(confirmedMessageResult.txpk.ipol);
            Assert.Equal("LORA", confirmedMessageResult.txpk.modu);

            // Expected changes to fcnt:
            // FcntDown => expectedFcntDown
            Assert.Equal(initialFcntUp, loRaDevice.FCntUp);
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);            
            Assert.True(loRaDevice.HasFrameCountChanges);


            // message should not be sent to iot hub
            loRaDeviceClient.Verify(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()), Times.Never);
            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// This tests the multi gateway scenario where a 2nd gateway cannot find the joined device because IoT Hub twin has not yet been updated
        /// It device api will not find it, only once the device registry finds it the message will be sent to IoT Hub
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task When_Second_Gateway_Does_Not_Find_Device_Should_Keep_Trying_On_Subsequent_Requests()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1));
            const string devAddr = "02AABBCC";
            const string nwkSKey = "00000000000000000000000000000002";
            const string appSKey = "00000000000000000000000000000001";
            var devEUI = simulatedDevice.DevEUI;

            simulatedDevice.SetupJoin(appSKey, nwkSKey, devAddr);

            var updatedTwin = TestUtils.CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { TwinProperty.AppEUI, simulatedDevice.AppEUI },
                    { TwinProperty.AppKey, simulatedDevice.AppKey },
                    { TwinProperty.SensorDecoder, nameof(LoRaPayloadDecoder.DecoderValueSensor) },
                },
                reported: new Dictionary<string, object>
                {
                    { TwinProperty.AppSKey, appSKey },
                    { TwinProperty.NwkSKey, nwkSKey },
                    { TwinProperty.DevAddr, devAddr },
                    { TwinProperty.DevNonce, "ABCD" },
                });

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Twin will be loaded once
            deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(updatedTwin);

            // Will check received messages once
            deviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>())).ReturnsAsync((Message)null);

            // Will send the 3 unconfirmed message
            deviceClient.Setup(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    Assert.NotNull(t.data);
                    Assert.IsType<JObject>(t.data);
                    Assert.Equal("3", ((JObject)t.data)["value"].ToString());

                })
                .ReturnsAsync(true);


            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // Will try to find the iot device based on dev addr            
            loRaDeviceApi.SetupSequence(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult())
                .ReturnsAsync(new SearchDevicesResult())
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(
                this.ServerConfiguration, 
                new MemoryCache(new MemoryCacheOptions()), 
                loRaDeviceApi.Object,
                new TestLoRaDeviceFactory(deviceClient.Object));


            var messageProcessor = new MessageProcessor(
               this.ServerConfiguration,
               deviceRegistry,
               new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object),
               new LoRaPayloadDecoder()
               );


            // Unconfirmed message #1 should fail
            var unconfirmedRxpk1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1)
                .SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            Assert.Null(await messageProcessor.ProcessMessageAsync(unconfirmedRxpk1));

            // Unconfirmed message #2 should fail
            var unconfirmedRxpk2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 2)
                .SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            Assert.Null(await messageProcessor.ProcessMessageAsync(unconfirmedRxpk2));

            // Unconfirmed message #3 should succeed
            var unconfirmedRxpk3 = simulatedDevice.CreateUnconfirmedDataUpMessage("3", fcnt: 3)
                .SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            Assert.Null(await messageProcessor.ProcessMessageAsync(unconfirmedRxpk3));

            deviceClient.VerifyAll();
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
        public async Task ABP_Confirmed_Message_Should_Use_Rchf_0(string deviceGatewayID, uint rfch)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntDown = 20;
            simulatedDevice.FrmCntUp = 100;

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);           

            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("1234");
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            rxpk.rfch = rfch;
            var confirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.NotNull(confirmedMessageResult);
            Assert.Equal((uint)0, confirmedMessageResult.txpk.rfch);
            Assert.Equal(RegionFactory.CreateEU868Region().GetDownstreamChannel(rxpk), confirmedMessageResult.txpk.freq);
            Assert.Equal("4/5", confirmedMessageResult.txpk.codr);
            Assert.False(confirmedMessageResult.txpk.imme);
            Assert.True(confirmedMessageResult.txpk.ipol);
            Assert.Equal("LORA", confirmedMessageResult.txpk.modu);

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();            
        }


        [Theory]
        [InlineData(ServerGatewayID, 1600)]
        [InlineData(ServerGatewayID, 2000)]
        [InlineData(ServerGatewayID, 5000)]
        [InlineData(null, 1600)]
        [InlineData(null, 2000)]
        [InlineData(null, 5000)]
        public async Task When_Sending_Unconfirmed_Message_To_IoT_Hub_Takes_Too_Long_Should_Not_Check_For_C2D(
            string deviceGatewayID,
            int sendMessageDelayInMs)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback(() => Thread.Sleep(TimeSpan.FromMilliseconds(sendMessageDelayInMs)))
                .ReturnsAsync(true);          

            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello");
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);

            loRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());
            
            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }
    


        /// <summary>
        /// Verifies that if the update twin takes too long that no join accepts are sent
        /// </summary>
        /// <param name="deviceGatewayID"></param>
        /// <returns></returns>
        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task ABP_When_Getting_Twin_Fails_Should_Work_On_Retry(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));           
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;
            var devAddr = simulatedDevice.DevAddr;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = simulatedDevice.CreateABPTwin();
            loRaDeviceClient.SetupSequence(x => x.GetTwinAsync())
                .ReturnsAsync((Twin)null)
                .ReturnsAsync(twin);

            // 1 message will be sent
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) =>
                 {
                    Assert.Equal(2, t.fcnt);
                    Assert.Equal("2", ((JObject)t.data)["value"].ToString());
                 })
                 .ReturnsAsync(true);

            // will check for c2d msg
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);
            
            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
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

            // send 1st unconfirmed message, get twin will fail
            var unconfirmedMessage1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            var unconfirmedMessage1Rxpk = unconfirmedMessage1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var unconfirmedMessageResult1 = await messageProcessor.ProcessMessageAsync(unconfirmedMessage1Rxpk);
            Assert.Null(unconfirmedMessageResult1);

            var devicesInCache = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Empty(devicesInCache);


            // sends 2nd unconfirmed message, now get twin will work
            var unconfirmedMessage2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 2);
            var unconfirmedMessage2Rxpk = unconfirmedMessage2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            var unconfirmedMessageResult2 = await messageProcessor.ProcessMessageAsync(unconfirmedMessage2Rxpk);
            Assert.Null(unconfirmedMessageResult2);

            devicesInCache = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Single(devicesInCache);
            Assert.True(devicesInCache.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(simulatedDevice.NwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(simulatedDevice.AppSKey, loRaDevice.AppSKey);
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(2, loRaDevice.FCntUp);

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();

        }


        /// <summary>
        /// Tests that a ABP device (already in cached or not), receives 1st message with invalid mic, 2nd with valid
        /// should send message 2 to iot hub
        /// </summary>
        /// <returns></returns>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ABP_When_First_Message_Has_Invalid_Mic_Second_Should_Send_To_Hub(bool isAlreadyInDeviceRegistryCache)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            const int firstMessageFcnt = 3;
            const int secondMessageFcnt = 4;
            const string wrongNwkSKey = "00000000000000000000000000001234";
            var unconfirmedMessageWithWrongMic = simulatedDevice.CreateUnconfirmedDataUpMessage("123", fcnt: firstMessageFcnt).SerializeUplink(simulatedDevice.AppSKey, wrongNwkSKey).rxpk[0];
            var unconfirmedMessageWithCorrectMic = simulatedDevice.CreateUnconfirmedDataUpMessage("456", fcnt: secondMessageFcnt).SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            if (!isAlreadyInDeviceRegistryCache)
            {
                loRaDeviceClient.Setup(x => x.GetTwinAsync())
                    .ReturnsAsync(simulatedDevice.CreateABPTwin());
            }


            // C2D message will be checked
            loRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // will search for the device twice
            loRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(loRaDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(loRaDevice.DevAddr, loRaDevice.DevEUI, "aaa").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            if (isAlreadyInDeviceRegistryCache)
            {
                var dictionary = new DevEUIToLoRaDeviceDictionary();
                dictionary[loRaDevice.DevEUI] = loRaDevice;
                memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);
            }

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            // first message should fail
            Assert.Null(await messageProcessor.ProcessMessageAsync(unconfirmedMessageWithWrongMic));

            // second message should succeed
            Assert.Null(await messageProcessor.ProcessMessageAsync(unconfirmedMessageWithCorrectMic));


            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<JObject>(loRaDeviceTelemetry.data);
            var telemetryData = (JObject)loRaDeviceTelemetry.data;
            Assert.Equal("456", telemetryData["value"].ToString());

            var devicesByDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.NotEmpty(devicesByDevAddr);
            Assert.True(devicesByDevAddr.TryGetValue(simulatedDevice.DevEUI, out var loRaDeviceFromRegistry));
            Assert.Equal(secondMessageFcnt, loRaDeviceFromRegistry.FCntUp);
            Assert.True(loRaDeviceFromRegistry.IsOurDevice);

            loRaDeviceApi.VerifyAll();
            loRaDeviceClient.VerifyAll();
        }       
    }
}
