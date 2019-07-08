// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
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
    using Xunit;

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
            uint payloadFcntUp,
            uint deviceInitialFcntUp,
            uint deviceInitialFcntDown)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntDown = deviceInitialFcntDown;
            simulatedDevice.FrmCntUp = deviceInitialFcntUp;

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // twin will be updated with new fcnt
            int? fcntUpSavedInTwin = null;
            int? fcntDownSavedInTwin = null;

            // twin should be saved only if not starting at 0, 0
            var shouldSaveTwin = deviceInitialFcntDown != 0 || deviceInitialFcntUp != 0;
            if (shouldSaveTwin)
            {
                // Twin will be saved
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .Callback<TwinCollection>((t) =>
                    {
                        fcntUpSavedInTwin = (int)t[TwinProperty.FCntUp];
                        fcntDownSavedInTwin = (int)t[TwinProperty.FCntDown];
                    })
                    .ReturnsAsync(true);
            }

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(devEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcntUp);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Ensure that a telemetry was sent
            Assert.NotNull(loRaDeviceTelemetry);
            // Assert.Equal(msgPayload, loRaDeviceTelemetry.data);

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
            Assert.Equal(0U, loRaDevice.FCntDown); // fctn down will always be set to zero
            if (payloadFcntUp == 0)
                Assert.False(loRaDevice.HasFrameCountChanges); // no changes
            else
                Assert.True(loRaDevice.HasFrameCountChanges); // there are pending changes (fcntUp 0 => 1)

            // will update api in multi gateway scenario
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Verify(x => x.ABPFcntCacheResetAsync(devEUI, It.IsAny<uint>(), It.IsNotNull<string>()), Times.Exactly(1));
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID, "1234", "")]
        [InlineData(ServerGatewayID, "hello world", null)]
        [InlineData(null, "hello world", null)]
        [InlineData(null, "1234", "")]
        public async Task ABP_Unconfirmed_With_No_Decoder_Sends_Raw_Payload(string deviceGatewayID, string msgPayload, string sensorDecoder)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = sensorDecoder;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(loRaDevice.DevEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<UndecodedPayload>(loRaDeviceTelemetry.Data);
            var undecodedPayload = (UndecodedPayload)loRaDeviceTelemetry.Data;
            var expectedPayloadContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
            Assert.Equal(expectedPayloadContent, undecodedPayload.Value);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Unconfirmed_Sends_Valid_Mac_Commands_As_Part_Of_Payload_And_Receives_Answer_As_Part_Of_Payload()
        {
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed mac LinkCheckCmd
            string msgPayload = "02";
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1, isHexPayload: true, fport: 0);
            // only use nwkskey
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.NwkSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should reply with MacCommand Answer
            Assert.NotNull(request.ResponseDownlink);
            LoRaPayloadData data = new LoRaPayloadData(Convert.FromBase64String(request.ResponseDownlink.Txpk.Data));
            Assert.True(data.CheckMic(simulatedDevice.NwkSKey));
            data.PerformEncryption(simulatedDevice.NwkSKey);
            data.Frmpayload.Span.Reverse();
            LoRaTools.LinkCheckAnswer link = new LoRaTools.LinkCheckAnswer(data.Frmpayload.Span);
            Assert.NotNull(link);
            Assert.Equal(1, (int)link.GwCnt);
            Assert.Equal(15, (int)link.Margin);
            // Nothing should be sent to IoT Hub
            Assert.Null(loRaDeviceTelemetry);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Unconfirmed_Sends_Valid_Mac_Commands_In_Fopts_And_Reply_In_Fopts()
        {
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            Dictionary<string, string> eventProperties = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), It.IsNotNull<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) =>
                {
                    loRaDeviceTelemetry = t;
                    eventProperties = d;
                })
                .ReturnsAsync(true);

            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "Hello",
                Fport = 1,
            };

            var cloudToDeviceMessage = c2d.CreateMessage();

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed mac LinkCheckCmd
            string msgPayload = "Hello World";
            List<LoRaTools.MacCommand> macCommands = new List<LoRaTools.MacCommand>()
            {
                new LoRaTools.LinkCheckRequest()
            };
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1, macCommands: macCommands);
            // only use nwkskey
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.NwkSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should reply with MacCommand Answer
            Assert.NotNull(request.ResponseDownlink);
            LoRaPayloadData data = new LoRaPayloadData(Convert.FromBase64String(request.ResponseDownlink.Txpk.Data));
            Assert.True(data.CheckMic(simulatedDevice.NwkSKey));
            // FOpts are not encrypted
            LoRaTools.LinkCheckAnswer link = new LoRaTools.LinkCheckAnswer(data.Fopts.Span);
            Assert.NotNull(link);
            Assert.NotNull(eventProperties);
            Assert.Contains("LinkCheckCmd", eventProperties.Keys);
            Assert.Equal(1, (int)link.GwCnt);
            Assert.Equal(15, (int)link.Margin);
            // Nothing should be sent to IoT Hub
            Assert.NotNull(loRaDeviceTelemetry);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData("00")]
        [InlineData("25")]
        public async Task ABP_Unconfirmed_Sends_Invalid_Mac_Commands_In_Fopts(string macCommand)
        {
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    loRaDeviceTelemetry = t;
                })
                .ReturnsAsync(true);

            var c2dMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "Hello",
                Fport = 1,
            };

            var cloudToDeviceMessage = c2dMessage.CreateMessage();

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed mac LinkCheckCmd
            string msgPayload = "Hello World";

            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            unconfirmedMessagePayload.Fopts = ConversionHelper.StringToByteArray(macCommand);
            unconfirmedMessagePayload.Fctrl = new byte[1] { (byte)unconfirmedMessagePayload.Fopts.Length };
            // only use nwkskey
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.NwkSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should reply with MacCommand Answer
            Assert.NotNull(request.ResponseDownlink);
            LoRaPayloadData data = new LoRaPayloadData(Convert.FromBase64String(request.ResponseDownlink.Txpk.Data));
            Assert.True(data.CheckMic(simulatedDevice.NwkSKey));
            // FOpts are not encrypted
            var payload = data.GetDecryptedPayload(simulatedDevice.AppSKey);
            string c2dreceivedPayload = Encoding.UTF8.GetString(payload);
            Assert.Equal(c2dMessage.Payload, c2dreceivedPayload);
            // Nothing should be sent to IoT Hub
            Assert.NotNull(loRaDeviceTelemetry);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData("00")]
        [InlineData("26")]
        public async Task ABP_Unconfirmed_Sends_Invalid_Mac_Commands_As_Part_Of_Payload(string macCommand)
        {
            string deviceGatewayID = ServerGatewayID;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = string.Empty;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed mac LinkCheckCmd
            string msgPayload = macCommand;
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1, isHexPayload: true, fport: 0);
            // only use nwkskey
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.NwkSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            // Server should not reply and discard the message
            Assert.Null(request.ResponseDownlink);
            Assert.Null(loRaDeviceTelemetry);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 1)]
        [InlineData(255, 1)]
        [InlineData(16777215, 16777215)]
        [InlineData(127, 127)]
        [InlineData(255, 255)]
        public async Task ABP_Device_NetId_Should_Match_Server(uint deviceNetId, uint serverNetId)
        {
            string msgPayload = "1234";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, netId: deviceNetId));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = null;

            // message will be sent if there is a match
            bool netIdMatches = deviceNetId == serverNetId;
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            if (netIdMatches)
            {
                this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                    .ReturnsAsync(true);

                // C2D message will be checked
                this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                    .ReturnsAsync((Message)null);

                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(loRaDevice.DevEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);
            }

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);
            this.ServerConfiguration.NetId = serverNetId;
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            if (netIdMatches)
            {
                Assert.NotNull(loRaDeviceTelemetry);
                Assert.IsType<UndecodedPayload>(loRaDeviceTelemetry.Data);
                var undecodedPayload = (UndecodedPayload)loRaDeviceTelemetry.Data;
                var expectedPayloadContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(msgPayload));
                Assert.Equal(expectedPayloadContent, undecodedPayload.Value.ToString());
            }
            else
            {
                Assert.Null(loRaDeviceTelemetry);
                Assert.Equal(0U, loRaDevice.FCntUp);
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID, null)]
        [InlineData(ServerGatewayID, "test")]
        [InlineData(ServerGatewayID, "test", "idtest")]
        public async Task When_Ack_Message_Received_Should_Be_In_Msg_Properties(string deviceGatewayID, string data, string msgId = null)
        {
            const uint initialFcntUp = 100;
            const uint payloadFcnt = 102;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntUp = initialFcntUp;

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            if (msgId != null)
                loRaDevice.LastConfirmedC2DMessageID = msgId;

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) =>
                {
                    Assert.NotNull(d);
                    Assert.True(d.ContainsKey(Constants.C2D_MSG_PROPERTY_VALUE_NAME));

                    if (msgId == null)
                        Assert.True(d.ContainsValue(Constants.C2D_MSG_ID_PLACEHOLDER));
                    else
                        Assert.True(d.ContainsValue(msgId));
                })
                .Returns(Task.FromResult(true));

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var messageDispatcher = new MessageDispatcher(
               this.ServerConfiguration,
               deviceRegistry,
               this.FrameCounterUpdateStrategyProvider);

            var ackMessage = simulatedDevice.CreateUnconfirmedDataUpMessage(data, fcnt: payloadFcnt, fctrl: (byte)FctrlEnum.Ack);
            var ackRxpk = ackMessage.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var ackRequest = new WaitableLoRaRequest(ackRxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(ackRequest);
            Assert.True(await ackRequest.WaitCompleteAsync());
            Assert.Null(ackRequest.ResponseDownlink);
            Assert.True(deviceRegistry.InternalGetCachedDevicesForDevAddr(loRaDevice.DevAddr).TryGetValue(loRaDevice.DevEUI, out var loRaDeviceInfo));

            Assert.Equal(payloadFcnt, loRaDeviceInfo.FCntUp);
        }

        [Theory]
        [InlineData(ServerGatewayID, 21)]
        [InlineData(null, 21)]
        [InlineData(null, 30)]
        public async Task When_ConfirmedUp_Message_With_Same_Fcnt_Should_Return_Ack_And_Not_Send_To_Hub(string deviceGatewayID, uint expectedFcntDown)
        {
            const uint initialFcntUp = 100;
            const uint initialFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntUp = initialFcntUp;
            simulatedDevice.FrmCntDown = initialFcntDown;

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api

            // in multigateway scenario the device api will be called to resolve fcntDown
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(req => req.ClientFCntDown == initialFcntDown && req.ClientFCntUp == initialFcntUp && req.GatewayId == this.ServerConfiguration.GatewayID)))
                    .ReturnsAsync(() => new FunctionBundlerResult
                    {
                        AdrResult = new LoRaTools.ADR.LoRaADRResult
                        {
                            CanConfirmToDevice = true,
                            NbRepetition = 1,
                            TxPower = 0,
                            FCntDown = expectedFcntDown,
                        },
                        NextFCntDown = expectedFcntDown
                    });
            }

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("repeat", fcnt: 100);
            var rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var confirmedRequest = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(confirmedRequest);

            // ack should be received
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.NotNull(confirmedRequest.ResponseDownlink.Txpk);
            Assert.Single(this.PacketForwarder.DownlinkMessages);

            var confirmedMessageResult = this.PacketForwarder.DownlinkMessages[0];

            // validates txpk according to eu region
            Assert.True(RegionManager.EU868.TryGetUpstreamChannelFrequency(rxpk, out double frequency));
            Assert.Equal(frequency, confirmedMessageResult.Txpk.Freq);
            Assert.Equal("4/5", confirmedMessageResult.Txpk.Codr);
            Assert.False(confirmedMessageResult.Txpk.Imme);
            Assert.True(confirmedMessageResult.Txpk.Ipol);
            Assert.Equal("LORA", confirmedMessageResult.Txpk.Modu);

            // Expected changes to fcnt:
            // FcntDown => expectedFcntDown
            Assert.Equal(initialFcntUp, loRaDevice.FCntUp);
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.True(loRaDevice.HasFrameCountChanges);

            // message should not be sent to iot hub
            this.LoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()), Times.Never);
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// This tests the multi gateway scenario where a 2nd gateway cannot find the joined device because IoT Hub twin has not yet been updated
        /// It device api will not find it, only once the device registry finds it the message will be sent to IoT Hub
        /// </summary>
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

            // Twin will be loaded once
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(updatedTwin);

            // Will check received messages once
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>())).ReturnsAsync((Message)null);

            // Will send the 3 unconfirmed message
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsAny<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    Assert.NotNull(t.Data);
                    Assert.IsType<DecodedPayloadValue>(t.Data);
                    Assert.Equal("3", ((DecodedPayloadValue)t.Data).Value.ToString());
                })
                .ReturnsAsync(true);

            // Will try to find the iot device based on dev addr
            this.LoRaDeviceApi.SetupSequence(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult())
                .ReturnsAsync(new SearchDevicesResult())
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(
                this.ServerConfiguration,
                new MemoryCache(new MemoryCacheOptions()),
                this.LoRaDeviceApi.Object,
                this.LoRaDeviceFactory);

            // Making the reload interval zero
            deviceRegistry.DevAddrReloadInterval = TimeSpan.Zero;

            var messageDispatcher = new MessageDispatcher(
               this.ServerConfiguration,
               deviceRegistry,
               this.FrameCounterUpdateStrategyProvider);

            // Unconfirmed message #1 should fail
            var unconfirmedRxpk1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1)
                .SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var unconfirmedRequest1 = new WaitableLoRaRequest(unconfirmedRxpk1, this.PacketForwarder);
            messageDispatcher.DispatchRequest(unconfirmedRequest1);
            Assert.True(await unconfirmedRequest1.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest1.ResponseDownlink);

            // wait 10ms so that loader is removed
            await Task.Delay(10);

            // Unconfirmed message #2 should fail
            var unconfirmedRxpk2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 2)
                .SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var unconfirmedRequest2 = new WaitableLoRaRequest(unconfirmedRxpk2, this.PacketForwarder);
            messageDispatcher.DispatchRequest(unconfirmedRequest2);
            Assert.True(await unconfirmedRequest2.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest2.ResponseDownlink);

            // wait 10ms so that loader is removed
            await Task.Delay(10);

            // Unconfirmed message #3 should succeed
            var unconfirmedRxpk3 = simulatedDevice.CreateUnconfirmedDataUpMessage("3", fcnt: 3)
                .SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var unconfirmedRequest3 = new WaitableLoRaRequest(unconfirmedRxpk3, this.PacketForwarder);
            messageDispatcher.DispatchRequest(unconfirmedRequest3);
            Assert.True(await unconfirmedRequest3.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest3.ResponseDownlink);

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
        public async Task ABP_Confirmed_Message_Should_Use_Rchf_0(string deviceGatewayID, uint rfch)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntDown = 20;
            simulatedDevice.FrmCntUp = 100;

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var messagePayload = simulatedDevice.CreateConfirmedDataUpMessage("1234");
            var rxpk = messagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            rxpk.Rfch = rfch;
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var txpk = this.PacketForwarder.DownlinkMessages[0].Txpk;
            Assert.Equal(0U, txpk.Rfch);
            Assert.True(RegionManager.EU868.TryGetUpstreamChannelFrequency(rxpk, out double frequency));
            Assert.Equal(frequency, txpk.Freq);
            Assert.Equal("4/5", txpk.Codr);
            Assert.False(txpk.Imme);
            Assert.True(txpk.Ipol);
            Assert.Equal("LORA", txpk.Modu);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID, 1601)]
        [InlineData(ServerGatewayID, 2000)]
        [InlineData(ServerGatewayID, 5000)]
        [InlineData(null, 1600)]
        [InlineData(null, 2000)]
        [InlineData(null, 5000)]
        public async Task When_Sending_Unconfirmed_Message_To_IoT_Hub_Takes_Too_Long_Should_Not_Check_For_C2D(
            string deviceGatewayID,
            int delayInMs)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello");
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder, DateTime.UtcNow.Subtract(TimeSpan.FromMilliseconds(delayInMs)));
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            this.LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// Verifies that if the update twin takes too long that no join accepts are sent
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task ABP_When_Getting_Twin_Fails_Should_Work_On_Retry(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;
            var devAddr = simulatedDevice.DevAddr;

            // Device twin will be queried
            var twin = simulatedDevice.CreateABPTwin();
            this.LoRaDeviceClient.SetupSequence(x => x.GetTwinAsync())
                .ReturnsAsync((Twin)null)
                .ReturnsAsync(twin);

            // 1 message will be sent
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), It.IsAny<Dictionary<string, string>>()))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) =>
                 {
                     Assert.Equal(2, t.Fcnt);
                     Assert.Equal("2", ((DecodedPayloadValue)t.Data).Value.ToString());
                 })
                 .ReturnsAsync(true);

            // will check for c2d msg
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // first device client will be disposed
            this.LoRaDeviceClient.Setup(x => x.Dispose());

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Setting the interval in which we search for devices with same devAddr on server
            deviceRegistry.DevAddrReloadInterval = TimeSpan.Zero;

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // send 1st unconfirmed message, get twin will fail
            var unconfirmedMessage1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 1);
            var unconfirmedMessage1Rxpk = unconfirmedMessage1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request1 = this.CreateWaitableRequest(unconfirmedMessage1Rxpk);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.Empty(this.PacketForwarder.DownlinkMessages);

            var devicesInCache = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Empty(devicesInCache);

            // Wait 50ms so loader can be removed from cache
            await Task.Delay(50);

            // sends 2nd unconfirmed message, now get twin will work
            var unconfirmedMessage2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 2);
            var unconfirmedMessage2Rxpk = unconfirmedMessage2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request2 = this.CreateWaitableRequest(unconfirmedMessage2Rxpk);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.Empty(this.PacketForwarder.DownlinkMessages);

            devicesInCache = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Single(devicesInCache);
            Assert.True(devicesInCache.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(simulatedDevice.NwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(simulatedDevice.AppSKey, loRaDevice.AppSKey);
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(2U, loRaDevice.FCntUp);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// Tests that a ABP device (already in cached or not), receives 1st message with invalid mic, 2nd with valid
        /// should send message 2 to iot hub
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ABP_When_First_Message_Has_Invalid_Mic_Second_Should_Send_To_Hub(bool isAlreadyInDeviceRegistryCache)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            if (!isAlreadyInDeviceRegistryCache)
            {
                this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                    .ReturnsAsync(simulatedDevice.CreateABPTwin());
            }

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // will search for the device twice
            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(loRaDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(loRaDevice.DevAddr, loRaDevice.DevEUI, "aaa").AsList()));

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            if (isAlreadyInDeviceRegistryCache)
            {
                var dictionary = new DevEUIToLoRaDeviceDictionary();
                dictionary[loRaDevice.DevEUI] = loRaDevice;
                memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);
            }

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // first message should fail
            const int firstMessageFcnt = 3;
            const string wrongNwkSKey = "00000000000000000000000000001234";
            var unconfirmedMessageWithWrongMic = simulatedDevice.CreateUnconfirmedDataUpMessage("123", fcnt: firstMessageFcnt).SerializeUplink(simulatedDevice.AppSKey, wrongNwkSKey).Rxpk[0];
            var requestWithWrongMic = this.CreateWaitableRequest(unconfirmedMessageWithWrongMic);
            messageDispatcher.DispatchRequest(requestWithWrongMic);
            Assert.True(await requestWithWrongMic.WaitCompleteAsync());
            Assert.Null(requestWithWrongMic.ResponseDownlink);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck, requestWithWrongMic.ProcessingFailedReason);

            // second message should succeed
            const uint secondMessageFcnt = 4;
            var unconfirmedMessageWithCorrectMic = simulatedDevice.CreateUnconfirmedDataUpMessage("456", fcnt: secondMessageFcnt).SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var requestWithCorrectMic = this.CreateWaitableRequest(unconfirmedMessageWithCorrectMic);
            messageDispatcher.DispatchRequest(requestWithCorrectMic);
            Assert.True(await requestWithCorrectMic.WaitCompleteAsync());
            Assert.Null(requestWithCorrectMic.ResponseDownlink);

            Assert.NotNull(loRaDeviceTelemetry);
            Assert.IsType<DecodedPayloadValue>(loRaDeviceTelemetry.Data);
            var telemetryData = (DecodedPayloadValue)loRaDeviceTelemetry.Data;
            Assert.Equal("456", telemetryData.Value.ToString());

            var devicesByDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.NotEmpty(devicesByDevAddr);
            Assert.True(devicesByDevAddr.TryGetValue(simulatedDevice.DevEUI, out var loRaDeviceFromRegistry));
            Assert.Equal(secondMessageFcnt, loRaDeviceFromRegistry.FCntUp);
            Assert.True(loRaDeviceFromRegistry.IsOurDevice);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        /// <summary>
        /// Tests that a ABP device without AppSKey should not send message to IoT Hub
        /// </summary>
        [Theory]
        [InlineData(TwinProperty.AppSKey)]
        [InlineData(TwinProperty.NwkSKey)]
        [InlineData(TwinProperty.DevAddr)]
        public async Task ABP_When_AppSKey_Or_NwkSKey_Or_DevAddr_Is_Missing_Should_Not_Send_Message_To_Hub(string missingProperty)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.SensorDecoder = "DecoderValueSensor";

            // will get the device twin without AppSKey
            var twin = TestUtils.CreateABPTwin(simulatedDevice);
            twin.Properties.Desired[missingProperty] = null;
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                    .ReturnsAsync(twin);

            // Lora device api

            // will search for the device twice
            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(loRaDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(loRaDevice.DevAddr, loRaDevice.DevEUI, "aaa").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // message should not be processed
            var request = this.CreateWaitableRequest(simulatedDevice.CreateUnconfirmedMessageUplink("1234").Rxpk[0]);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            var devicesByDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.Empty(devicesByDevAddr);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_ConfirmedUp_Message_Is_Resubmitted_Should_Ack_3_Times(string deviceGatewayID)
        {
            const uint deviceInitialFcntUp = 100;
            const uint deviceInitialFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice.FrmCntUp = deviceInitialFcntUp;
            simulatedDevice.FrmCntDown = deviceInitialFcntDown;

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // We will send two messages
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.Is<LoRaDeviceTelemetry>(t => t.Fcnt == deviceInitialFcntUp + 1), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.Is<LoRaDeviceTelemetry>(t => t.Fcnt == deviceInitialFcntUp + 2), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(true);

            var sb = new StringBuilder();
            // in multigateway scenario the device api will be called to resolve fcntDown
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 1 }, NextFCntDown = deviceInitialFcntDown + 1 });

                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 1 && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 2 }, NextFCntDown = deviceInitialFcntDown + 2 });

                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 2 && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 3 }, NextFCntDown = deviceInitialFcntDown + 3 });

                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 3 && r.ClientFCntUp == deviceInitialFcntUp + 1)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 4 }, NextFCntDown = deviceInitialFcntDown + 4 });

                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 4 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 5 }, NextFCntDown = deviceInitialFcntDown + 5 });

                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 5 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 6 }, NextFCntDown = deviceInitialFcntDown + 6 });

                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 6 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 7 }, NextFCntDown = deviceInitialFcntDown + 7 });

                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.Is<FunctionBundlerRequest>(r => r.ClientFCntDown == deviceInitialFcntDown + 7 && r.ClientFCntUp == deviceInitialFcntUp + 2)))
                            .ReturnsAsync((string s, FunctionBundlerRequest req) => new FunctionBundlerResult { AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, NbRepetition = 1, TxPower = 0, FCntDown = deviceInitialFcntDown + 8 }, NextFCntDown = deviceInitialFcntDown + 8 });
            }

            // add device to cache already
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            dictionary[loRaDevice.DevEUI] = loRaDevice;
            memoryCache.Set<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, dictionary);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends confirmed message
            var firstMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("repeat", fcnt: deviceInitialFcntUp + 1);
            var firstMessageRxpk = firstMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            // 1x as new fcntUp and 3x as resubmit
            for (var i = 0; i < 4; i++)
            {
                var firstMessageRequest = this.CreateWaitableRequest(firstMessageRxpk);
                messageDispatcher.DispatchRequest(firstMessageRequest);
                Assert.True(await firstMessageRequest.WaitCompleteAsync());

                // ack should be received
                Assert.NotNull(firstMessageRequest.ResponseDownlink);
                Assert.NotNull(firstMessageRequest.ResponseDownlink.Txpk);
                Assert.Equal(i + 1, this.PacketForwarder.DownlinkMessages.Count);
            }

            // resubmitting should fail
            var fourthRequest = this.CreateWaitableRequest(firstMessageRxpk);
            messageDispatcher.DispatchRequest(fourthRequest);
            Assert.True(await fourthRequest.WaitCompleteAsync());
            Assert.Null(fourthRequest.ResponseDownlink);
            Assert.Equal(4, this.PacketForwarder.DownlinkMessages.Count);
            Assert.Equal(LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded, fourthRequest.ProcessingFailedReason);

            // Sending the next fcnt with failed messages should work, including resubmit
            var secondMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("repeat", fcnt: deviceInitialFcntUp + 2);
            var secondMessageRxpk = secondMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            // 1x as new fcntUp and 3x as resubmit
            for (var i = 0; i < 4; i++)
            {
                var request = this.CreateWaitableRequest(secondMessageRxpk);
                messageDispatcher.DispatchRequest(request);
                Assert.True(await request.WaitCompleteAsync());

                // ack should be received
                Assert.NotNull(request.ResponseDownlink);
                Assert.NotNull(request.ResponseDownlink.Txpk);
                Assert.Equal(i + 5, this.PacketForwarder.DownlinkMessages.Count);
            }

            // resubmitting should fail
            var resubmitSecondRequest = this.CreateWaitableRequest(secondMessageRxpk);
            messageDispatcher.DispatchRequest(resubmitSecondRequest);
            Assert.True(await resubmitSecondRequest.WaitCompleteAsync());
            Assert.Null(resubmitSecondRequest.ResponseDownlink);
            Assert.Equal(8, this.PacketForwarder.DownlinkMessages.Count);
            Assert.Equal(LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded, resubmitSecondRequest.ProcessingFailedReason);

            Assert.Equal(2 + deviceInitialFcntUp, loRaDevice.FCntUp);
            Assert.Equal(8 + deviceInitialFcntDown, loRaDevice.FCntDown);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Device_With_Invalid_NetId_Should_Not_Load_Devices()
        {
            string msgPayload = "1234";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, netId: 0));
            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk1 = unconfirmedMessagePayload1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request1 = new WaitableLoRaRequest(rxpk1, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.InvalidNetId, request1.ProcessingFailedReason);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, fcnt: 2);
            var rxpk2 = unconfirmedMessagePayload2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request2 = new WaitableLoRaRequest(rxpk2, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.True(request2.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.InvalidNetId, request2.ProcessingFailedReason);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task ABP_Device_With_Invalid_NetId_In_Allowed_DevAdr_Should_Be_Accepted()
        {
            string msgPayload = "1234";
            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, netId: 0, gatewayID: ServerGatewayID));

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // Add this device to the allowed dev address list
            this.ServerConfiguration.AllowedDevAddresses = new HashSet<string>(1)
            {
                simulatedDevice.DevAddr
            };

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // device api will be searched for payload
            var searchDevicesResult = new SearchDevicesResult(new[]
            {
                new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "device1"),
                new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "device2"),
            });

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(searchDevicesResult);

            deviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(simulatedDevice.CreateABPTwin());

            deviceClient.Setup(x => x.Disconnect())
               .Returns(true);

            deviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
               .ReturnsAsync((Message)null);

            this.LoRaDeviceFactory.SetClient(simulatedDevice.DevEUI, deviceClient.Object);
            deviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .ReturnsAsync(true);

            var device1SentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            deviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
             .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => device1SentTelemetry.Add(loRaDeviceTelemetry))
             .ReturnsAsync(true);

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, fcnt: 1);
            var rxpk1 = unconfirmedMessagePayload1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request1 = new WaitableLoRaRequest(rxpk1, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingSucceeded);
            Assert.NotNull(request1.ResponseDownlink);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, fcnt: 2);
            var rxpk2 = unconfirmedMessagePayload2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request2 = new WaitableLoRaRequest(rxpk2, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.NotNull(request2.ResponseDownlink);
            Assert.True(request2.ProcessingSucceeded);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(1)] // ABP with soft reset
        [InlineData(11)]
        public async Task When_Loading_Multiple_Devices_With_Same_DevAddr_Should_Add_All_To_Cache_And_Process_Message(uint payloadFcntUp)
        {
            var isResetingDevice = payloadFcntUp <= 1;
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID), frmCntDown: 5, frmCntUp: 10);

            var devAddr = simulatedDevice1.LoRaDevice.DevAddr;

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(2, gatewayID: ServerGatewayID), frmCntDown: 6, frmCntUp: 10);
            simulatedDevice2.DevAddr = devAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // Device client 1
            // - Get Twin
            // - Update twin (if isResetingDevice)
            // - Send event
            // - Check c2d message
            var device1SentTelemetry = new List<LoRaDeviceTelemetry>();
            var deviceClient1 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient1.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => device1SentTelemetry.Add(loRaDeviceTelemetry))
                .ReturnsAsync(true);

            deviceClient1.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            deviceClient1.Setup(x => x.GetTwinAsync()).ReturnsAsync(simulatedDevice1.CreateABPTwin());

            if (isResetingDevice)
            {
                deviceClient1.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .ReturnsAsync(true);
            }

            // Device client 2
            // - Get Twin
            var deviceClient2 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient2.Setup(x => x.GetTwinAsync()).ReturnsAsync(simulatedDevice2.CreateABPTwin());

            // device api will be searched for payload
            var searchDevicesResult = new SearchDevicesResult(new[]
            {
                new IoTHubDeviceInfo(simulatedDevice1.DevAddr, simulatedDevice1.DevEUI, "device1"),
                new IoTHubDeviceInfo(simulatedDevice2.DevAddr, simulatedDevice2.DevEUI, "device2"),
            });

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(searchDevicesResult);

            this.LoRaDeviceFactory.SetClient(simulatedDevice1.DevEUI, deviceClient1.Object);
            this.LoRaDeviceFactory.SetClient(simulatedDevice2.DevEUI, deviceClient2.Object);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice1.CreateUnconfirmedDataUpMessage("1", fcnt: payloadFcntUp);
            var rxpk1 = unconfirmedMessagePayload1.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey).Rxpk[0];
            var request1 = new WaitableLoRaRequest(rxpk1, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingSucceeded);
            Assert.Single(device1SentTelemetry);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice1.CreateUnconfirmedDataUpMessage("2", fcnt: payloadFcntUp + 1);
            var rxpk2 = unconfirmedMessagePayload2.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey).Rxpk[0];
            var request2 = new WaitableLoRaRequest(rxpk2, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.True(request2.ProcessingSucceeded);
            Assert.Equal(2, device1SentTelemetry.Count);

            // Ensure that the devices have been cached
            var cachedDevices = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice1.DevAddr);
            Assert.Equal(2, cachedDevices.Count);
            Assert.True(cachedDevices.TryGetValue(simulatedDevice1.DevEUI, out var loRaDevice1));

            // If the fcnt made a reset (0-1) the fcntdown is zero
            if (isResetingDevice)
            {
                Assert.Equal(0U, loRaDevice1.FCntDown);
            }
            else
            {
                Assert.Equal(simulatedDevice1.FrmCntDown + Constants.MAX_FCNT_UNSAVED_DELTA - 1U, loRaDevice1.FCntDown);
            }

            Assert.Equal(payloadFcntUp + 1, loRaDevice1.FCntUp);

            Assert.True(cachedDevices.TryGetValue(simulatedDevice2.DevEUI, out var loRaDevice2));
            Assert.Equal(simulatedDevice2.FrmCntUp, loRaDevice2.FCntUp);
            Assert.Equal(simulatedDevice2.FrmCntDown + Constants.MAX_FCNT_UNSAVED_DELTA - 1U, loRaDevice2.FCntDown);

            deviceClient1.VerifyAll();
            deviceClient2.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // devices were loaded only once
            this.LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(It.IsAny<string>()), Times.Once());
            deviceClient1.Verify(x => x.GetTwinAsync(), Times.Once());
            deviceClient2.Verify(x => x.GetTwinAsync(), Times.Once());
        }

        [Theory]
        [InlineData(1)] // ABP with soft reset
        [InlineData(11)]
        public async Task When_Loading_Multiple_Devices_With_Same_DevAddr_One_Fails_Should_Add_One_To_Cache_And_Process_Message(uint payloadFcntUp)
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            var devEUI = simulatedDevice1.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice1.LoRaDevice.DevAddr;

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(2, gatewayID: ServerGatewayID));
            simulatedDevice2.DevAddr = devAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;

            // Device client 1
            // - Get Twin
            // - Update twin (if isResetingDevice)
            // - Send event
            // - Check c2d message
            var device1SentTelemetry = new List<LoRaDeviceTelemetry>();
            var deviceClient1 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient1.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => device1SentTelemetry.Add(loRaDeviceTelemetry))
                .ReturnsAsync(true);

            deviceClient1.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            deviceClient1.Setup(x => x.GetTwinAsync()).ReturnsAsync(simulatedDevice1.CreateABPTwin());

            // Device client 2
            // - Get Twin -> throws TimeoutException
            var deviceClient2 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient2.Setup(x => x.GetTwinAsync()).ThrowsAsync(new TimeoutException(), TimeSpan.FromMilliseconds(100));

            // device api will be searched for payload
            var searchDevicesResult = new SearchDevicesResult(new[]
            {
                new IoTHubDeviceInfo(simulatedDevice1.DevAddr, simulatedDevice1.DevEUI, "device1"),
                new IoTHubDeviceInfo(simulatedDevice2.DevAddr, simulatedDevice2.DevEUI, "device2"),
            });

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(searchDevicesResult);

            this.LoRaDeviceFactory.SetClient(simulatedDevice1.DevEUI, deviceClient1.Object);
            this.LoRaDeviceFactory.SetClient(simulatedDevice2.DevEUI, deviceClient2.Object);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message #1
            var unconfirmedMessagePayload1 = simulatedDevice1.CreateUnconfirmedDataUpMessage("1", fcnt: payloadFcntUp);
            var rxpk1 = unconfirmedMessagePayload1.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey).Rxpk[0];
            var request1 = new WaitableLoRaRequest(rxpk1, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingSucceeded);
            Assert.Single(device1SentTelemetry);

            // sends unconfirmed message #2
            var unconfirmedMessagePayload2 = simulatedDevice1.CreateUnconfirmedDataUpMessage("2", fcnt: payloadFcntUp + 1);
            var rxpk2 = unconfirmedMessagePayload2.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey).Rxpk[0];
            var request2 = new WaitableLoRaRequest(rxpk2, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.True(request2.ProcessingSucceeded);
            Assert.Equal(2, device1SentTelemetry.Count);

            // Ensure that the device has been cached
            var cachedDevices = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice1.DevAddr);
            Assert.Single(cachedDevices);
            Assert.True(cachedDevices.TryGetValue(simulatedDevice1.DevEUI, out var loRaDevice1));

            // If the fcnt made a reset (0-1) the fcntdown is zero
            if (payloadFcntUp <= 1)
            {
                Assert.Equal(0U, loRaDevice1.FCntDown);
            }
            else
            {
                Assert.Equal(Constants.MAX_FCNT_UNSAVED_DELTA - 1U, loRaDevice1.FCntDown);
            }

            Assert.Equal(payloadFcntUp + 1, loRaDevice1.FCntUp);

            deviceClient1.VerifyAll();
            deviceClient2.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // devices were loaded only once
            this.LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(devAddr), Times.Once());
            deviceClient1.Verify(x => x.GetTwinAsync(), Times.Once());
            deviceClient2.Verify(x => x.GetTwinAsync(), Times.Once());
        }

        [Fact]
        public async Task When_Upstream_Is_Empty_Should_Call_Decoder_And_Send_Event_To_IoTHub()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            var devEUI = simDevice.LoRaDevice.DeviceID;
            var devAddr = simDevice.LoRaDevice.DevAddr;

            var loRaDevice = this.CreateLoRaDevice(simDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true)
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    Assert.NotNull(t.Data);
                    Assert.Equal(1, t.Port);
                    Assert.Equal("fport_1_decoded", t.Data.ToString());
                });

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>(MockBehavior.Strict);
            payloadDecoder.Setup(x => x.DecodeMessageAsync(devEUI, It.IsAny<byte[]>(), 1, It.IsAny<string>()))
                .ReturnsAsync(new DecodePayloadResult("fport_1_decoded"))
                .Callback<string, byte[], byte, string>((_, data, fport, decoder) =>
                {
                    Assert.Equal(1, fport);

                    // input data is empty
                    Assert.Null(data);
                });
            this.PayloadDecoder.SetDecoder(payloadDecoder.Object);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload1 = simDevice.CreateUnconfirmedDataUpMessage(null, fcnt: 4);
            var rxpk1 = unconfirmedMessagePayload1.SerializeUplink(simDevice.AppSKey, simDevice.NwkSKey).Rxpk[0];
            var request1 = new WaitableLoRaRequest(rxpk1, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingSucceeded);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            payloadDecoder.VerifyAll();
        }

        [Theory]
        [InlineData(10, 9)]
        [InlineData(10, 10)]
        public async Task When_Upstream_Fcnt_Is_Lower_Or_Equal_To_Device_Should_Discard_Message(uint devFcntUp, uint payloadFcnt)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            simulatedDevice.FrmCntUp = devFcntUp;

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(cachedDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcnt);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.InvalidFrameCounter, request.ProcessingFailedReason);

            // verify that the device in device registry has correct properties and frame counters
            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Single(devicesForDevAddr);
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEUI, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(devFcntUp, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown); // fctn down will always be set to zero
            Assert.False(loRaDevice.HasFrameCountChanges); // no changes

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task When_Receiving_Device_Message_And_Loading_Device_Fails_Second_Message_Should_Be_Processed()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            simDevice.FrmCntDown = 10;
            simDevice.FrmCntUp = 50;

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            this.LoRaDeviceClient.SetupSequence(x => x.GetTwinAsync())
                .ThrowsAsync(new TimeoutException())
                .ReturnsAsync(simDevice.CreateABPTwin());

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request1 = this.CreateWaitableRequest(simDevice.CreateUnconfirmedMessageUplink("1").Rxpk[0]);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr, request1.ProcessingFailedReason);

            // give time for the loader to be removed from cache
            await Task.Delay(50);

            var request2 = this.CreateWaitableRequest(simDevice.CreateUnconfirmedMessageUplink("2").Rxpk[0]);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingSucceeded);

            this.LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(simDevice.DevAddr), Times.Exactly(2));
            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task When_Receiving_Relax_Reset_Fcnt_Of_New_Device_Should_Not_Save_Fcnt(
            [CombinatorialValues(null, ServerGatewayID)] string gatewayID,
            [CombinatorialValues(0, 1)] uint payloadFcnt)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: gatewayID));

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            if (string.IsNullOrEmpty(gatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(simDevice.DevEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateABPTwin());

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request1 = this.CreateWaitableRequest(simDevice.CreateUnconfirmedMessageUplink("1", fcnt: payloadFcnt).Rxpk[0]);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingSucceeded);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();

            this.LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
        }
    }
}