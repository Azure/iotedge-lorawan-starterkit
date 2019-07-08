// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
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
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
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
                                // Todo check
                                CanConfirmToDevice = false,
                                FCntDown = simulatedDevice.FrmCntDown + 1,
                                NbRepetition = 1,
                                TxPower = 0
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

        [Theory]
        [InlineData(9, 10)]
        [InlineData(9, 19)]
        public async Task OTAA_Unconfirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message(uint initialDeviceFcntUp, uint payloadFcnt)
        {
            const uint InitialDeviceFcntDown = 20;
            var needsToSaveFcnt = payloadFcnt - initialDeviceFcntUp >= Constants.MAX_FCNT_UNSAVED_DELTA;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: initialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (needsToSaveFcnt)
            {
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .ReturnsAsync(true);
            }

            var cloudToDeviceMessageBody = new ReceivedLoRaCloudToDeviceMessage()
            {
                Fport = 1,
                Payload = "c2d"
            };

            var cloudToDeviceMessage = cloudToDeviceMessageBody.CreateMessage();
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

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt);
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
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has pending changes?
            if (needsToSaveFcnt)
                Assert.False(loraDevice.HasFrameCountChanges);
            else
                Assert.True(loraDevice.HasFrameCountChanges);
        }

        [Theory]
        [InlineData(9, 10)]
        [InlineData(9, 19)]
        public async Task OTAA_Confirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message(uint initialDeviceFcntUp, uint payloadFcnt)
        {
            const uint InitialDeviceFcntDown = 20;
            var needsToSaveFcnt = payloadFcnt - initialDeviceFcntUp >= Constants.MAX_FCNT_UNSAVED_DELTA;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: initialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (needsToSaveFcnt)
            {
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                   .ReturnsAsync(true);
            }

            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = 1 }
                .CreateMessage();

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

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: payloadFcnt);
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
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has pending changes?
            if (needsToSaveFcnt)
                Assert.False(loraDevice.HasFrameCountChanges);
            else
                Assert.True(loraDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_In_Time_For_First_Window_Should_Send_Downstream_In_First_Window()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

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

            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = 1 }
                .CreateMessage();

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

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
            var euRegion = RegionManager.EU868;
            Assert.True(euRegion.TryGetUpstreamChannelFrequency(rxpk, out double frequency));
            // Ensure we are using second window frequency
            Assert.Equal(frequency, txpk.Freq);

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
            var expectedFcntDown = InitialDeviceFcntDown + Constants.MAX_FCNT_UNSAVED_DELTA; // adding 10 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loRaDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_Too_Late_For_First_Window_Should_Send_Downstream_In_Second_Window()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

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

            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = 1 }
                .CreateMessage();

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(this.EmptyAdditionalMessageReceiveAsync);

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk, constantElapsedTime: TestUtils.GetStartTimeOffsetForSecondWindow());
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
            var euRegion = RegionManager.EU868;
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
            var expectedFcntDown = InitialDeviceFcntDown + Constants.MAX_FCNT_UNSAVED_DELTA; // adding 10 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loRaDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task When_Device_Prefers_Second_Window_Should_Send_Downstream_In_Second_Window()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

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

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = 1 }
                .CreateMessage();

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(this.EmptyAdditionalMessageReceiveAsync); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

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
            var euRegion = RegionManager.EU868;
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
            var expectedFcntDown = InitialDeviceFcntDown + Constants.MAX_FCNT_UNSAVED_DELTA - 1 + 1; // adding 9 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.GetFcnt());
            Assert.Equal(0U, loRaDevice.FCntDown - loRaDevice.LastSavedFCntDown);

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

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = 1 }
                .CreateMessage();

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
            var request = this.CreateWaitableRequest(
                rxpk,
                constantElapsedTime: sendEventDurationInMs > 0 ? TimeSpan.FromMilliseconds(sendEventDurationInMs) : TimeSpan.FromMilliseconds(100));
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            var actualDownlink = this.PacketForwarder.DownlinkMessages.First();

            var euRegion = RegionManager.EU868;
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
            const uint PayloadFcnt = 20;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = msg,
                MacCommands = new[]
                {
                    new DevStatusRequest(),
                },
            };

            if (msg != string.Empty)
            {
                c2d.Fport = 1;
            }

            var cloudToDeviceMessage = c2d.CreateMessage();

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
        [InlineData("test", 1)]
        [InlineData("DevStatusCmd", null)]
        [InlineData("DevStatusCmd", 0)]
        public async Task OTAA_Unconfirmed_With_Cloud_To_Device_Mac_Command_Fails_Due_To_Wrong_Setup(string mac, int? fport)
        {
            const int PayloadFcnt = 20;
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

            var c2dJson = $"{{\"fport\":{fport}, \"payload\":\"asd\", \"macCommands\": [ {{ \"cid\": \"{mac}\" }}] }}";

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes(c2dJson));

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.RejectAsync(cloudToDeviceMessage))
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

        [Theory]
        [InlineData(null)]
        [InlineData("0000000000000001")]
        public async Task Unconfirmed_Cloud_To_Device_From_Decoder_Should_Send_Downstream_Message(string c2dDevEUI)
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var decoderResult = new DecodePayloadResult("1")
            {
                CloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
                {
                    Fport = 1,
                    MessageId = "123",
                    Payload = "12",
                    DevEUI = c2dDevEUI
                },
            };

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>(MockBehavior.Strict);
            payloadDecoder.Setup(x => x.DecodeMessageAsync(simulatedDevice.DevEUI, It.IsNotNull<byte[]>(), 1, It.IsAny<string>()))
                .ReturnsAsync(decoderResult);
            this.PayloadDecoder.SetDecoder(payloadDecoder.Object);

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

            // 6. Frame count has pending changes
            Assert.True(loraDevice.HasFrameCountChanges);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();

            payloadDecoder.VerifyAll();
        }

        [Fact]
        public async Task When_Takes_Too_Long_Receiving_First_C2D_Should_Abandon_Message()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

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

            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = 1 }
                .CreateMessage();

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage, TimeSpan.FromMilliseconds(2001));

            this.LoRaDeviceClient.Setup(x => x.AbandonAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

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

            // 2. No downstream message
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Empty(this.PacketForwarder.DownlinkMessages);

            // 3. Device FcntDown did change
            var devices = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.True(devices.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            Assert.Equal(InitialDeviceFcntDown + Constants.MAX_FCNT_UNSAVED_DELTA, loRaDevice.FCntDown);
        }

        [Fact]
        public async Task When_Takes_Too_Long_Getting_FcntDown_Should_Abandon_Message()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: null),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr;
            var devEUI = simulatedDevice.DevEUI;

            // Will get twin to initialize LoRaDevice
            var deviceTwin = TestUtils.CreateABPTwin(simulatedDevice);
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(deviceTwin);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = 1 }
                .CreateMessage();

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage);

            this.LoRaDeviceClient.Setup(x => x.AbandonAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            this.LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, InitialDeviceFcntDown, PayloadFcnt, this.ServerConfiguration.GatewayID))
                .ReturnsAsync((ushort)(InitialDeviceFcntDown + 1), TimeSpan.FromMilliseconds(2001));

            var loRaDeviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

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

            // 2. No downstream message
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Empty(this.PacketForwarder.DownlinkMessages);

            // 3. Device FcntDown did change
            var devices = loRaDeviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.True(devices.TryGetValue(simulatedDevice.DevEUI, out var loRaDevice));
            Assert.Equal(InitialDeviceFcntDown + 1, loRaDevice.FCntDown);
        }
    }
}