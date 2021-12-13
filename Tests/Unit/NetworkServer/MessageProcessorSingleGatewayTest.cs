// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.LoRaPhysical;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    /// <summary>
    /// Single gateway message processor tests.
    /// </summary>
    public class MessageProcessorSingleGatewayTest : MessageProcessorTestBase
    {
        [Theory]
        [InlineData(0)]
        [InlineData(2100)]
        public async Task Unknown_Device_Should_Not_Send_Messages(int searchDevicesDelayMs)
        {
            // Setup
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            if (searchDevicesDelayMs > 0)
            {
                LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr))
                    .Returns(Task.Delay(searchDevicesDelayMs).ContinueWith((_) => new SearchDevicesResult(), TaskScheduler.Default));
            }
            else
            {
                LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr))
                    .ReturnsAsync(new SearchDevicesResult());
            }

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Returns null
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr, request.ProcessingFailedReason);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2100)]
        public async Task When_Payload_Has_Invalid_Mic_Should_Not_Send_Messages(int searchDevicesDelayMs)
        {
            // Setup
            const string wrongSKey = "00000000000000000000000000EEDDFF";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var searchResult = new SearchDevicesResult(new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "1321").AsList());
            if (searchDevicesDelayMs > 0)
            {
                LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr))
                    .ReturnsAsync(searchResult, TimeSpan.FromMilliseconds(searchDevicesDelayMs));
            }
            else
            {
                LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr))
                    .ReturnsAsync(searchResult);
            }

            LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(TestUtils.CreateABPTwin(simulatedDevice));

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // Send request #1
            var payload1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 2);
            using var request1 = CreateWaitableRequest(payload1.SerializeUplink(simulatedDevice.AppSKey, wrongSKey).Rxpk[0]);
            messageProcessor.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.Null(request1.ResponseDownlink);
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck, request1.ProcessingFailedReason);

            await Task.Delay(2000);
            Assert.Single(deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr));

            // Send request #2
            var payload2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 3);
            using var request2 = CreateWaitableRequest(payload2.SerializeUplink(simulatedDevice.AppSKey, wrongSKey).Rxpk[0]);
            messageProcessor.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.Null(request2.ResponseDownlink);
            Assert.True(request2.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck, request2.ProcessingFailedReason);
            Assert.Single(deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr));

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();

            LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr), Times.Exactly(1));
        }

        [Fact]
        public async Task Unknown_Region_Should_Return_Null()
        {
            // Setup
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            rxpk.Freq = 0;

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(rxpk, useRealTimer: true);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Returns null
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.InvalidRegion, request.ProcessingFailedReason);
        }

        [Fact]
        public async Task ABP_Unconfirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 10);
            simulatedDevice.FrmCntUp = 9;

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = NewNonEmptyCache(loraDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);

            // 2. Return is null (there is nothing to send downstream)
            Assert.Null(request.ResponseDownlink);

            // 3. Frame counter up was updated
            Assert.Equal(10U, loraDevice.FCntUp);
        }

        [Fact]
        public void When_Faulty_MAC_Message_Is_Received_Processing_Abort_Without_Infinite_Loop()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 10);
            simulatedDevice.FrmCntUp = 9;

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = NewNonEmptyCache(loraDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var request = new LoRaRequest(
                new Rxpk
                {
                    Data = "QDDaAAGxfh0FAI6wAENHbvgt1UK5Je1uPo/bLPB9HlnOXLGlLRUrTtA0KOHrZhusGl+L4g=="
                },
                null,
                DateTime.Now);

            messageProcessor.DispatchRequest(request);
        }

        [Fact]
        public async Task OTAA_Confirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_DownstreamMessage()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID));
            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            using var cache = NewNonEmptyCache(loraDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(PacketForwarder.DownlinkMessages);
            var downlinkMessage = PacketForwarder.DownlinkMessages.First();
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), global::LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Frame counter up was updated
            Assert.Equal(1U, loraDevice.FCntUp);

            // 5. Frame counter down was incremented
            Assert.Equal(1U, loraDevice.FCntDown);
            Assert.Equal(1, MemoryMarshal.Read<ushort>(payloadDataDown.Fcnt.Span));
        }

        [Fact]
        public async Task OTAA_Unconfirmed_Message_With_Fcnt_Change_Of_10_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            const uint PayloadFcnt = 19;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = NewNonEmptyCache(loraDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            using var request = CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. Return nothing
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is not changed
            Assert.Equal(InitialDeviceFcntDown, loraDevice.FCntDown);

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }

        private static bool IsTwinFcntZero(TwinCollection t) => (int)t[TwinProperty.FCntDown] == 0 && (int)t[TwinProperty.FCntUp] == 0;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task When_ABP_Device_With_Relaxed_FrameCounter_Has_FCntUP_Zero_Or_One_Should_Reset_Counter_And_Process_Message(uint payloadFCnt)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID));

            // generate payload with frame count 0 or 1
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFCnt);

            simulatedDevice.FrmCntDown = 0;
            simulatedDevice.FrmCntUp = 10;

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            // Will send the event to IoT Hub
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            // will try to get C2D message
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>())).ReturnsAsync((Message)null);

            // Will save the fcnt up/down to zero
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.Is<TwinCollection>((t) => IsTwinFcntZero(t))))
                .ReturnsAsync(true);

            using var cache = NewNonEmptyCache(loraDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 3. Return is null (there is nothing to send downstream)
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            // 4. Frame counter up was updated
            Assert.Equal(payloadFCnt, loraDevice.FCntUp);
        }

        [Fact]
        public async Task ABP_From_Another_Gateway_Unconfirmed_Message_Should_Load_Device_Cache_And_Disconnect()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "another-gateway"))
            {
                FrmCntUp = 9
            };

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "1234").AsList()));

            LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(TestUtils.CreateABPTwin(simulatedDevice));

            LoRaDeviceClient.Setup(x => x.Disconnect())
                .Returns(true);

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 10);
            var rxpk1 = payload1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            using var request1 = CreateWaitableRequest(rxpk1);
            messageProcessor.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request1.ProcessingFailedReason);

            // Let loading finish
            await Task.Delay(50);

            // device should be cached
            var cachedDevices = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.Single(cachedDevices);
            Assert.True(cachedDevices.TryGetValue(simulatedDevice.DevEUI, out var cachedDevice));
            Assert.Equal(9U, cachedDevice.FCntUp);

            var payload2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 11);
            var rxpk2 = payload2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            using var request2 = CreateWaitableRequest(rxpk2);
            messageProcessor.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request2.ProcessingFailedReason);

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr), Times.Once());

            // 2. Frame counter up was not updated
            cachedDevices = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.Single(cachedDevices);
            cachedDevice = null;
            Assert.True(cachedDevices.TryGetValue(simulatedDevice.DevEUI, out cachedDevice));
            Assert.Equal(9U, cachedDevice.FCntUp);
        }

        [Fact]
        public async Task When_New_ABP_Device_Instance_Is_Created_Should_Increment_FCntDown()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            // device will:
            // - be initialized
            // - send event
            // - receive c2d
            LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 2);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            using var request = CreateWaitableRequest(rxpk, constantElapsedTime: TimeSpan.FromMilliseconds(300));
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // Wait until loader updates the device cache
            await Task.Delay(50);

            var devices = deviceRegistry.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.Single(devices);
            Assert.True(devices.TryGetValue(simulatedDevice.DevEUI, out var cachedDevice));
            Assert.True(cachedDevice.IsOurDevice);
            Assert.Equal(Constants.MaxFcntUnsavedDelta - 1U, cachedDevice.FCntDown);
            Assert.Equal(payload.GetFcnt(), (ushort)cachedDevice.FCntUp);

            // Device was searched by DevAddr
            LoRaDeviceApi.VerifyAll();

            // Device was created by factory
            LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(0U, null, null)]
        [InlineData(0U, 1U, 1U)]
        [InlineData(0U, 100U, 20U)]
        [InlineData(1U, null, null)]
        [InlineData(1U, 1U, 1U)]
        [InlineData(1U, 100U, 20U)]
        public async Task When_ABP_New_Loaded_Device_With_Fcnt_1_Or_0_Should_Reset_Fcnt_And_Send_To_IotHub(
            uint payloadFcntUp,
            uint? deviceTwinFcntUp,
            uint? deviceTwinFcntDown)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // twin will be loaded
            var initialTwin = new Twin();
            initialTwin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            initialTwin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            initialTwin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            initialTwin.Properties.Desired[TwinProperty.NwkSKey] = simulatedDevice.LoRaDevice.NwkSKey;
            initialTwin.Properties.Desired[TwinProperty.AppSKey] = simulatedDevice.LoRaDevice.AppSKey;
            initialTwin.Properties.Desired[TwinProperty.DevAddr] = devAddr;
            initialTwin.Properties.Desired[TwinProperty.GatewayID] = ServerConfiguration.GatewayID;
            initialTwin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            if (deviceTwinFcntDown.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntDown] = deviceTwinFcntDown.Value;
            if (deviceTwinFcntUp.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntUp] = deviceTwinFcntUp.Value;

            LoRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(initialTwin);

            // twin will be updated with new fcnt
            uint? fcntUpSavedInTwin = null;
            uint? fcntDownSavedInTwin = null;

            var expectedToSaveTwin = deviceTwinFcntDown > 0 || deviceTwinFcntUp > 0;
            if (expectedToSaveTwin)
            {
                // Twin will be save (0, 0) only if it was not 0, 0
                LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .Callback<TwinCollection>((t) =>
                    {
                        fcntUpSavedInTwin = (uint)t[TwinProperty.FCntUp];
                        fcntDownSavedInTwin = (uint)t[TwinProperty.FCntDown];
                    })
                    .ReturnsAsync(true);
            }

            // device api will be searched for payload
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            // Send to message processor
            using var messageDispatcher = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcntUp);
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            using var request = CreateWaitableRequest(rxpk);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Ensure that a telemetry was sent
            Assert.NotNull(loRaDeviceTelemetry);
            // Assert.Equal(msgPayload, loRaDeviceTelemetry.data);

            // Ensure that the device twins were saved
            if (expectedToSaveTwin)
            {
                Assert.NotNull(fcntDownSavedInTwin);
                Assert.NotNull(fcntUpSavedInTwin);
                Assert.Equal(0U, fcntDownSavedInTwin.Value);
                Assert.Equal(0U, fcntUpSavedInTwin.Value);
            }

            // Adding the loaded devices to the cache can take a while, give it time
            await Task.Delay(50);

            // verify that the device in device registry has correct properties and frame counters
            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Single(devicesForDevAddr);
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEUI, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(payloadFcntUp, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);
            if (payloadFcntUp == 0)
                Assert.False(loRaDevice.HasFrameCountChanges); // no changes
            else
                Assert.True(loRaDevice.HasFrameCountChanges); // there are pending changes (fcntUp 0 => 1)

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
