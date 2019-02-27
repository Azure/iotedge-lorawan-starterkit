// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
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
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);
            cachedDevice.DownlinkEnabled = false;

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            this.LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
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
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            // multi gateway will ask for next fcnt down
            var isMultigateway = string.IsNullOrEmpty(deviceGatewayID);
            if (isMultigateway)
            {
                this.LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, simulatedDevice.FrmCntDown, payloadFcnt, this.ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)(simulatedDevice.FrmCntDown + 1));

                // if we run with ADR, we will combine the call with the bundler
                this.LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()))
                    .ReturnsAsync(() => new FunctionBundlerResult
                        {
                            AdrResult = new LoRaTools.ADR.LoRaADRResult
                            {
                                CanConfirmToDevice = true,
                                FCntDown = simulatedDevice.FrmCntDown + 1,
                            },
                            NextFCntDown = simulatedDevice.FrmCntDown + 1
                        });
            }

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);
            cachedDevice.DownlinkEnabled = false;

            var devEUIDeviceDict = new DevEUIToLoRaDeviceDictionary();
            devEUIDeviceDict.TryAdd(devEUI, cachedDevice);
            memoryCache.Set(devAddr, devEUIDeviceDict);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends confirmed message
            var rxpk = simulatedDevice.CreateConfirmedMessageUplink("1234", fcnt: payloadFcnt).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);

            this.LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            this.LoRaDeviceClient.VerifyAll();
            if (isMultigateway && ((LoRaPayloadData)request.Payload).IsAdrEnabled)
            {
                this.LoRaDeviceApi.Verify(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()));
            }
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

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[Constants.FPORT_MSG_PROPERTY_KEY] = "1";
            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
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

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[Constants.FPORT_MSG_PROPERTY_KEY] = "1";
            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
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

            // Will get twin to initialize LoRaDevice
            var deviceTwin = TestUtils.CreateABPTwin(simulatedDevice);
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(deviceTwin);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[Constants.FPORT_MSG_PROPERTY_KEY] = "1";
            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var txpk = downlinkMessage.Txpk;
            var euRegion = RegionFactory.CreateEU868Region();

            // Ensure we are using second window frequency
            Assert.Equal(euRegion.GetDownstreamChannelFrequency(rxpk), txpk.Freq);

            // Ensure we are using second window datr
            Assert.Equal(euRegion.GetDownstreamDR(rxpk), txpk.Datr);

            // Ensure tmst was computed to 1 second
            Assert.Equal(1000000, txpk.Tmst);

            // Get the device from registry
            var deviceDictionary = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.True(deviceDictionary.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loRaDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), ConversionHelper.StringToByteArray(loRaDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
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

            // Will get twin to initialize LoRaDevice
            var deviceTwin = TestUtils.CreateABPTwin(simulatedDevice);
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(deviceTwin);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(1100)).ContinueWith((_) => true));

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[Constants.FPORT_MSG_PROPERTY_KEY] = "1";
            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(this.EmptyAdditionalMessageReceiveAsync);

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages.First();
            var euRegion = RegionFactory.CreateEU868Region();
            var txpk = downlinkMessage.Txpk;

            // Ensure we are using second window frequency
            Assert.Equal(euRegion.RX2DefaultReceiveWindows.frequency, txpk.Freq);

            // Ensure we are using second window datr
            Assert.Equal(euRegion.DRtoConfiguration[euRegion.RX2DefaultReceiveWindows.dr].configuration, txpk.Datr);

            // Ensure tmst was computed to 2 seconds (2 windows in Europe)
            Assert.Equal(2000000, txpk.Tmst);

            // Get the device from registry
            var deviceDictionary = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.True(deviceDictionary.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loRaDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), ConversionHelper.StringToByteArray(loRaDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
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

            // Will get twin to initialize LoRaDevice
            var deviceTwin = TestUtils.CreateABPTwin(
                simulatedDevice,
                desiredProperties: new Dictionary<string, object>
                {
                    { TwinProperty.PreferredWindow, 2 }
                });

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(deviceTwin);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[Constants.FPORT_MSG_PROPERTY_KEY] = "1";
            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(this.EmptyAdditionalMessageReceiveAsync); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, new MemoryCache(new MemoryCacheOptions()), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 3. Return is downstream message
            Assert.True(request.ProcessingSucceeded);
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages.First();
            var euRegion = RegionFactory.CreateEU868Region();
            var txpk = downlinkMessage.Txpk;

            // Ensure we are using second window frequency
            Assert.Equal(euRegion.RX2DefaultReceiveWindows.frequency, txpk.Freq);

            // Ensure we are using second window datr
            Assert.Equal(euRegion.DRtoConfiguration[euRegion.RX2DefaultReceiveWindows.dr].configuration, txpk.Datr);

            // Ensure tmst was computed to 2 seconds (2 windows in Europe)
            Assert.Equal(2000000, txpk.Tmst);

            // Get the device from registry
            var deviceDictionary = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.True(deviceDictionary.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loRaDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), ConversionHelper.StringToByteArray(loRaDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
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
        [InlineData(1, 750, 690, 999, 2)]
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

            var sentEventAsyncSetup = this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null));
            if (sendEventDurationInMs > 0)
            {
                sentEventAsyncSetup.ReturnsAsync(true, TimeSpan.FromMilliseconds(sendEventDurationInMs));
            }
            else
            {
                sentEventAsyncSetup.ReturnsAsync(true);
            }

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[Constants.FPORT_MSG_PROPERTY_KEY] = "1";

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsInRange<TimeSpan>(TimeSpan.FromMilliseconds(checkMinDuration), TimeSpan.FromMilliseconds(checkMaxDuration), Range.Inclusive)))
                .ReturnsAsync(cloudToDeviceMessage);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage))
                .Returns(this.EmptyAdditionalMessageReceiveAsync); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.PreferredWindow = preferredWindow;

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            var actualDownlink = this.PacketForwarder.DownlinkMessages.First();

            var euRegion = RegionFactory.CreateEU868Region();
            if (expectedRX == Constants.RECEIVE_WINDOW_1)
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

        [Theory]
        [InlineData("")]
        [InlineData("test")]
        public async Task OTAA_Unconfirmed_With_Cloud_To_Device_Mac_Command_Returns_Downstream_Message(string msg)
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes(msg));

            if (msg != string.Empty)
            {
                cloudToDeviceMessage.Properties[Constants.FPORT_MSG_PROPERTY_KEY] = "1";
            }

            cloudToDeviceMessage.Properties[Constants.C2D_MSG_PROPERTY_MAC_COMMAND] = "DevStatusCmd";

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));

            // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
            payloadDataDown.PerformEncryption(msg == string.Empty ?
                    loraDevice.NwkSKey :
                    loraDevice.AppSKey);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);

            // 7. Mac commands should be present in reply
            // mac command is in fopts if there is a c2d message
            if (msg == string.Empty)
            {
                Assert.Equal(0, payloadDataDown.Fport.Span[0]);
                Assert.NotNull(payloadDataDown.Frmpayload.Span.ToArray());
                Assert.Single(payloadDataDown.Frmpayload.Span.ToArray());
                Assert.Equal((byte)LoRaTools.CidEnum.DevStatusCmd, payloadDataDown.Frmpayload.Span[0]);
            }
            else
            {
                Assert.NotNull(payloadDataDown.Fopts.Span.ToArray());
                Assert.Single(payloadDataDown.Fopts.Span.ToArray());
                Assert.Equal((byte)LoRaTools.CidEnum.DevStatusCmd, payloadDataDown.Fopts.Span[0]);
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData("test")]
        [InlineData("DevStatusCmd")]
        public async Task OTAA_Unconfirmed_With_Cloud_To_Device_Mac_Command_Fails_Due_To_Wrong_setup(string mac)
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("asd"));

            cloudToDeviceMessage.Properties[Constants.C2D_MSG_PROPERTY_MAC_COMMAND] = mac;

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. DownStream Message should be null as the processing should fail
            Assert.Null(request.ResponseDownlink);
        }
    }
}