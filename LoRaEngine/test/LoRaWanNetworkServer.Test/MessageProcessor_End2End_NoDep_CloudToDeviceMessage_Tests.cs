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
    // Cloud to device message processing tests (Join tests are handled in other class)
    public class MessageProcessor_End2End_NoDep_CloudToDeviceMessage_Tests : MessageProcessorTestBase
    {
        public MessageProcessor_End2End_NoDep_CloudToDeviceMessage_Tests()
        {
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Device_With_Downlink_Disabled_Received_Unconfirmed_Data_Should_Not_Check_For_C2D(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // using factory to create mock of
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            cachedDevice.DownlinkEnabled = false;

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(this.ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder());

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var unconfirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(unconfirmedMessageResult);

            loRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Device_With_Downlink_Disabled_Received_Confirmed_Data_Should_Not_Check_For_C2D(string deviceGatewayID)
        {
            const int payloadFcnt = 10;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            // Lora device api
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            // multi gateway will ask for next fcnt down
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                loRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, simulatedDevice.FrmCntDown, payloadFcnt, this.ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)(simulatedDevice.FrmCntDown + 1));
            }

            // using factory to create mock of
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            cachedDevice.DownlinkEnabled = false;

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(this.ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder());

            // sends confirmed message
            var rxpk = simulatedDevice.CreateConfirmedMessageUplink("1234", fcnt: payloadFcnt).Rxpk[0];
            var confirmedMessageResult = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.NotNull(confirmedMessageResult);

            loRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task OTAA_Unconfirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[MessageProcessor.FPORT_MSG_PROPERTY_KEY] = "1";
            loraDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            loraDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(new SingleGatewayFrameCounterUpdateStrategy());

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(actual);
            Assert.IsType<DownlinkPktFwdMessage>(actual);
            var downlinkMessage = (DownlinkPktFwdMessage)actual;
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task OTAA_Confirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[MessageProcessor.FPORT_MSG_PROPERTY_KEY] = "1";
            loraDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            loraDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(new SingleGatewayFrameCounterUpdateStrategy());

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(actual);
            Assert.IsType<DownlinkPktFwdMessage>(actual);
            var downlinkMessage = (DownlinkPktFwdMessage)actual;
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_In_Time_For_First_Window_Should_Send_Downstream_In_First_Window()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr;
            var devEUI = simulatedDevice.DevEUI;

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Will get twin to initialize LoRaDevice
            var deviceTwin = TestUtils.CreateABPTwin(simulatedDevice);
            loraDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(deviceTwin);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[MessageProcessor.FPORT_MSG_PROPERTY_KEY] = "1";
            loraDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            loraDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var loRaDeviceAPI = new Mock<LoRaDeviceAPIServiceBase>();
            loRaDeviceAPI.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), loRaDeviceAPI.Object, new TestLoRaDeviceFactory(loraDeviceClient.Object));

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                loRaDeviceRegistry,
                new LoRaDeviceFrameCounterUpdateStrategyFactory(this.ServerConfiguration.GatewayID, loRaDeviceAPI.Object),
                new LoRaPayloadDecoder());

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(actual);
            Assert.IsType<DownlinkPktFwdMessage>(actual);
            var downlinkMessage = (DownlinkPktFwdMessage)actual;
            var euRegion = RegionFactory.CreateEU868Region();

            // Ensure we are using second window frequency
            Assert.Equal(euRegion.GetDownstreamChannel(rxpk), actual.Txpk.Freq);

            // Ensure we are using second window datr
            Assert.Equal(euRegion.GetDownstreamDR(rxpk), actual.Txpk.Datr);

            // Ensure tmst was computed to 1 second
            Assert.Equal(1000000, actual.Txpk.Tmst);

            // Get the device from registry
            var deviceDictionary = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.True(deviceDictionary.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loRaDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), ConversionHelper.StringToByteArray(loRaDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loRaDevice.FCntUp);

            // 5. Frame counter down is updated
            var expectedFcntDown = InitialDeviceFcntDown + 10 + 1; // adding 10 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loRaDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_Too_Late_For_First_Window_Should_Send_Downstream_In_Second_Window()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr;
            var devEUI = simulatedDevice.DevEUI;

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Will get twin to initialize LoRaDevice
            var deviceTwin = TestUtils.CreateABPTwin(simulatedDevice);
            loraDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(deviceTwin);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(1100)).ContinueWith((_) => true));

            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[MessageProcessor.FPORT_MSG_PROPERTY_KEY] = "1";
            loraDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(this.EmptyAdditionalMessageReceiveAsync);

            loraDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var loRaDeviceAPI = new Mock<LoRaDeviceAPIServiceBase>();
            loRaDeviceAPI.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), loRaDeviceAPI.Object, new TestLoRaDeviceFactory(loraDeviceClient.Object));

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                loRaDeviceRegistry,
                new LoRaDeviceFrameCounterUpdateStrategyFactory(this.ServerConfiguration.GatewayID, loRaDeviceAPI.Object),
                new LoRaPayloadDecoder());

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(actual);
            Assert.IsType<DownlinkPktFwdMessage>(actual);
            var downlinkMessage = (DownlinkPktFwdMessage)actual;
            var euRegion = RegionFactory.CreateEU868Region();

            // Ensure we are using second window frequency
            Assert.Equal(euRegion.RX2DefaultReceiveWindows.frequency, actual.Txpk.Freq);

            // Ensure we are using second window datr
            Assert.Equal(euRegion.DRtoConfiguration[euRegion.RX2DefaultReceiveWindows.dr].configuration, actual.Txpk.Datr);

            // Ensure tmst was computed to 2 seconds (2 windows in Europe)
            Assert.Equal(2000000, actual.Txpk.Tmst);

            // Get the device from registry
            var deviceDictionary = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.True(deviceDictionary.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loRaDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), ConversionHelper.StringToByteArray(loRaDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loRaDevice.FCntUp);

            // 5. Frame counter down is updated
            var expectedFcntDown = InitialDeviceFcntDown + 10 + 1; // adding 10 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loRaDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_Device_Prefers_Second_Window_Should_Send_Downstream_In_Second_Window()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr;
            var devEUI = simulatedDevice.DevEUI;

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Will get twin to initialize LoRaDevice
            var deviceTwin = TestUtils.CreateABPTwin(
                simulatedDevice,
                desiredProperties: new Dictionary<string, object>
                {
                    { TwinProperty.PreferredWindow, 2 }
                });
            loraDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(deviceTwin);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[MessageProcessor.FPORT_MSG_PROPERTY_KEY] = "1";
            loraDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(this.EmptyAdditionalMessageReceiveAsync); // 2nd cloud to device message does not return anything

            loraDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var loRaDeviceAPI = new Mock<LoRaDeviceAPIServiceBase>();
            loRaDeviceAPI.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), loRaDeviceAPI.Object, new TestLoRaDeviceFactory(loraDeviceClient.Object));

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                loRaDeviceRegistry,
                new LoRaDeviceFrameCounterUpdateStrategyFactory(this.ServerConfiguration.GatewayID, loRaDeviceAPI.Object),
                new LoRaPayloadDecoder());

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(actual);
            Assert.IsType<DownlinkPktFwdMessage>(actual);
            var downlinkMessage = (DownlinkPktFwdMessage)actual;
            var euRegion = RegionFactory.CreateEU868Region();

            // Ensure we are using second window frequency
            Assert.Equal(euRegion.RX2DefaultReceiveWindows.frequency, actual.Txpk.Freq);

            // Ensure we are using second window datr
            Assert.Equal(euRegion.DRtoConfiguration[euRegion.RX2DefaultReceiveWindows.dr].configuration, actual.Txpk.Datr);

            // Ensure tmst was computed to 2 seconds (2 windows in Europe)
            Assert.Equal(2000000, actual.Txpk.Tmst);

            // Get the device from registry
            var deviceDictionary = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.True(deviceDictionary.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loRaDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), ConversionHelper.StringToByteArray(loRaDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loRaDevice.FCntUp);

            // 5. Frame counter down is updated
            var expectedFcntDown = InitialDeviceFcntDown + 10 + 1; // adding 10 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loRaDevice.HasFrameCountChanges);
        }

        [Theory]
        // Preferred Window: 1
        // - Aiming for RX1
        [InlineData(1, 0, 400, 610, 1)] // 1000 - (400 - noise)
        [InlineData(1, 100, 300, 510, 1)]
        [InlineData(1, 200, 200, 410, 1)]
        // - Aiming for RX2
        [InlineData(1, 700, 690, 999, 2)]
        [InlineData(1, 1000, 250, 610, 2)]

        // Preferred Window: 2
        // - Aiming for RX2
        [InlineData(2, 0, 1400, 1610, 2)]
        [InlineData(2, 100, 1300, 1510, 2)]
        public async Task When_Device_Checks_For_C2D_Message_Uses_Available_Time(
            int preferredWindow,
            int sendEventDurationInMs,
            int checkMinDuration,
            int checkMaxDuration,
            int expectedRX)
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr;
            var devEUI = simulatedDevice.DevEUI;

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            var sentEventAsyncSetup = loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null));
            sentEventAsyncSetup.Returns(() =>
            {
                if (sendEventDurationInMs > 0)
                {
                    var task = Task.Delay(sendEventDurationInMs).ContinueWith<bool>((_) => true);
                    task.ConfigureAwait(false);
                    return task;
                }

                return Task.FromResult(true);
            });

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[MessageProcessor.FPORT_MSG_PROPERTY_KEY] = "1";

            loraDeviceClient.Setup(x => x.ReceiveAsync(It.IsInRange<TimeSpan>(TimeSpan.FromMilliseconds(checkMinDuration), TimeSpan.FromMilliseconds(checkMaxDuration), Range.Inclusive)))
                .ReturnsAsync(cloudToDeviceMessage);

            loraDeviceClient.Setup(x => x.ReceiveAsync(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage))
                .Returns(this.EmptyAdditionalMessageReceiveAsync); // 2nd cloud to device message does not return anything

            loraDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var loRaDeviceAPI = new Mock<LoRaDeviceAPIServiceBase>();

            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);
            loRaDevice.PreferredWindow = preferredWindow;

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsNotNull<LoRaPayloadData>()))
                .ReturnsAsync(loRaDevice);

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), loRaDeviceAPI.Object, new TestLoRaDeviceFactory(loraDeviceClient.Object));

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                new LoRaDeviceFrameCounterUpdateStrategyFactory(this.ServerConfiguration.GatewayID, loRaDeviceAPI.Object),
                new LoRaPayloadDecoder());

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var actualDownlink = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.NotNull(actualDownlink);

            loraDeviceClient.VerifyAll();
            this.LoRaDeviceRegistry.VerifyAll();

            var euRegion = RegionFactory.CreateEU868Region();
            if (expectedRX == 1)
            {
                // ensure response is for RX1
                Assert.Equal(rxpk.Tmst + 1000000, actualDownlink.Txpk.Tmst);
            }
            else
            {
                // ensure response is for RX2
                Assert.Equal(rxpk.Tmst + 2000000, actualDownlink.Txpk.Tmst);
            }
        }
    }
}
