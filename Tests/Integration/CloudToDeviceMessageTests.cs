// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;
    using static LoRaWan.ReceiveWindowNumber;
    using IoTHubDeviceInfo = NetworkServer.IoTHubDeviceInfo;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Cloud to device message processing tests (Join tests are handled in other class)
    public class CloudToDeviceMessageTests : MessageProcessorTestBase
    {
        private const FramePort TestPort = FramePorts.App1;

        public CloudToDeviceMessageTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Device_With_Downlink_Disabled_Received_Unconfirmed_Data_Should_Not_Check_For_C2D(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            // message will be sent
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<DevEui>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);
            }

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            cachedDevice.DownlinkEnabled = false;

            DeviceCache.Register(cachedDevice);

            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Device_With_Downlink_Disabled_Received_Confirmed_Data_Should_Not_Check_For_C2D(string deviceGatewayID)
        {
            const int payloadFcnt = 10;
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var devEUI = simulatedDevice.LoRaDevice.DevEui;

            // message will be sent
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // multi gateway will ask for next fcnt down
            var isMultigateway = string.IsNullOrEmpty(deviceGatewayID);
            if (isMultigateway)
            {
                LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, simulatedDevice.FrmCntDown, payloadFcnt, ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)(simulatedDevice.FrmCntDown + 1));

                // if we run with ADR, we will combine the call with the bundler
                LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()))
                    .ReturnsAsync(() =>
                        new FunctionBundlerResult
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


            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            cachedDevice.DownlinkEnabled = false;

            DeviceCache.Register(cachedDevice);

            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends confirmed message
            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: payloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);

            LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Never());

            LoRaDeviceClient.VerifyAll();
            if (isMultigateway && ((LoRaPayloadData)request.Payload).IsDataRateNetworkControlled)
            {
                LoRaDeviceApi.Verify(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()));
            }
        }

        [Theory]
        [InlineData(9, 10)]
        [InlineData(9, 19)]
        public async Task OTAA_Unconfirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message(uint initialDeviceFcntUp, uint payloadFcnt)
        {
            const uint InitialDeviceFcntDown = 20;
            var needsToSaveFcnt = payloadFcnt - initialDeviceFcntUp >= NetworkServer.Constants.MaxFcntUnsavedDelta;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: initialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (needsToSaveFcnt)
            {
                LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }

            var cloudToDeviceMessageBody = new ReceivedLoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "c2d"
            };

            using var cloudToDeviceMessage = cloudToDeviceMessageBody.CreateMessage();
            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            payloadDataDown.Serialize(loraDevice.AppSKey.Value);
            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.Fcnt);

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
            var needsToSaveFcnt = payloadFcnt - initialDeviceFcntUp >= NetworkServer.Constants.MaxFcntUnsavedDelta;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: initialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (needsToSaveFcnt)
            {
                LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);
            }

            using var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = TestPort }
                .CreateMessage();

            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: payloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            payloadDataDown.Serialize(loraDevice.AppSKey.Value);
            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.Fcnt);

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
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr.Value;
            var devEUI = simulatedDevice.DevEUI;

            // Will get twin to initialize LoRaDevice
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(simulatedDevice.GetDefaultAbpTwin());

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            using var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = TestPort }
                .CreateMessage();

            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            using var cache = NewMemoryCache();
            await using var loRaDeviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            TestUtils.CheckDRAndFrequencies(request, downlinkMessage);


            // Get the device from cache
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));

            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            payloadDataDown.Serialize(loRaDevice.AppSKey.Value);
            Assert.Equal(payloadDataDown.DevAddr, loRaDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loRaDevice.FCntUp);

            // 5. Frame counter down is updated
            var expectedFcntDown = InitialDeviceFcntDown + NetworkServer.Constants.MaxFcntUnsavedDelta; // adding 10 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.Fcnt);

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
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr.Value;
            var devEUI = simulatedDevice.DevEUI;

            // Will get twin to initialize LoRaDevice
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(simulatedDevice.GetDefaultAbpTwin());

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            using var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = TestPort }
                .CreateMessage();

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(EmptyAdditionalMessageReceiveAsync);

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            using var cache = NewMemoryCache();
            await using var loRaDeviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload, constantElapsedTime: TestUtils.GetStartTimeOffsetForSecondWindow());
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages.First();

            TestUtils.CheckDRAndFrequencies(request, downlinkMessage, true);

            // Get the device from cache
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            payloadDataDown.Serialize(loRaDevice.AppSKey.Value);
            Assert.Equal(payloadDataDown.DevAddr, loRaDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loRaDevice.FCntUp);

            // 5. Frame counter down is updated
            var expectedFcntDown = InitialDeviceFcntDown + NetworkServer.Constants.MaxFcntUnsavedDelta; // adding 10 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.Fcnt);

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
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr.Value;
            var devEUI = simulatedDevice.DevEUI;

            // Will get twin to initialize LoRaDevice
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetAbpDesiredTwinProperties() with { PreferredWindow = ReceiveWindow2 },
                                             simulatedDevice.GetAbpReportedTwinProperties());

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            using var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = TestPort }
                .CreateMessage();

            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .Returns(EmptyAdditionalMessageReceiveAsync); // 2nd cloud to device message does not return anything

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            using var cache = NewMemoryCache();
            await using var loRaDeviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 3. Return is downstream message
            Assert.True(request.ProcessingSucceeded);
            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages.First();

            TestUtils.CheckDRAndFrequencies(request, downlinkMessage, true);

            // Get the device from cache
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            payloadDataDown.Serialize(loRaDevice.AppSKey.Value);
            Assert.Equal(payloadDataDown.DevAddr, loRaDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loRaDevice.FCntUp);

            // 5. Frame counter down is updated
            var expectedFcntDown = InitialDeviceFcntDown + NetworkServer.Constants.MaxFcntUnsavedDelta - 1 + 1; // adding 9 as buffer when creating a new device instance
            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(expectedFcntDown, payloadDataDown.Fcnt);
            Assert.Equal(0U, loRaDevice.FCntDown - loRaDevice.LastSavedFCntDown);

            // 6. Frame count has no pending changes
            Assert.False(loRaDevice.HasFrameCountChanges);
        }

        [Theory]
        // Preferred Window: 1
        // - Aiming for RX1
        [InlineData(ReceiveWindow1, 0, 400, 610, false)] // 1000 - (400 - noise)
        [InlineData(ReceiveWindow1, 100, 300, 510, false)]
        [InlineData(ReceiveWindow1, 200, 200, 410, false)]
        // - Aiming for RX2
        [InlineData(ReceiveWindow1, 750, 690, 999, true)]
        [InlineData(ReceiveWindow1, 1000, 250, 610, true)]

        // Preferred Window: 2
        // - Aiming for RX2
        [InlineData(ReceiveWindow2, 0, 1400, 1610, true)]
        [InlineData(ReceiveWindow2, 100, 1300, 1510, true)]
        public async Task When_Device_Checks_For_C2D_Message_Uses_Available_Time(
            ReceiveWindowNumber preferredWindow,
            int sendEventDurationInMs,
            int checkMinDuration,
            int checkMaxDuration,
            bool expectingSecondWindow)
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr;
            var devEUI = simulatedDevice.DevEUI;

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            using var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = TestPort }
                .CreateMessage();

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsInRange<TimeSpan>(TimeSpan.FromMilliseconds(checkMinDuration), TimeSpan.FromMilliseconds(checkMaxDuration), Moq.Range.Inclusive)))
                .ReturnsAsync(cloudToDeviceMessage);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage))
                .Returns(EmptyAdditionalMessageReceiveAsync); // 2nd cloud to device message does not return anything

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.PreferredWindow = preferredWindow;

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(
                payload,
                constantElapsedTime: sendEventDurationInMs > 0 ? TimeSpan.FromMilliseconds(sendEventDurationInMs) : TimeSpan.FromMilliseconds(100));
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.NotNull(request.ResponseDownlink);
            var downlinkMessage = Assert.Single(DownstreamMessageSender.DownlinkMessages);

            Assert.Equal(LoRaDeviceClassType.A, downlinkMessage.DeviceClassType);
            Assert.Equal(loRaDevice.ReportedRXDelay, downlinkMessage.LnsRxDelay);
            if (expectingSecondWindow)
            {
                Assert.Null(downlinkMessage.Rx1);
            }
            else
            {
                Assert.NotNull(downlinkMessage.Rx1);
            }

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
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
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = msg,
                MacCommands = { new DevStatusRequest() },
            };

            if (!string.IsNullOrEmpty(msg))
            {
                c2d.Fport = TestPort;
            }

            using var cloudToDeviceMessage = c2d.CreateMessage();

            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);

            // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
            if (string.IsNullOrEmpty(msg))
            {
                payloadDataDown.Serialize(loraDevice.NwkSKey.Value);
            }
            else
            {
                payloadDataDown.Serialize(loraDevice.AppSKey.Value);
            }

            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.Fcnt);

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);

            // 7. Mac commands should be present in reply
            // mac command is in fopts if there is a c2d message
            if (string.IsNullOrEmpty(msg))
            {
                Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
                Assert.NotNull(payloadDataDown.Frmpayload.Span.ToArray());
                Assert.Single(payloadDataDown.Frmpayload.Span.ToArray());
                Assert.Equal((byte)LoRaTools.Cid.DevStatusCmd, payloadDataDown.Frmpayload.Span[0]);
            }
            else
            {
                Assert.NotNull(payloadDataDown.Fopts.Span.ToArray());
                Assert.Single(payloadDataDown.Fopts.Span.ToArray());
                Assert.Equal((byte)LoRaTools.Cid.DevStatusCmd, payloadDataDown.Fopts.Span[0]);
            }

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
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
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var c2dJson = $"{{\"fport\":{fport}, \"payload\":\"asd\", \"macCommands\": [ {{ \"cid\": \"{mac}\" }}] }}";
            using var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes(c2dJson));

            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            LoRaDeviceClient.Setup(x => x.RejectAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload, constantElapsedTime: TimeSpan.Zero);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

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
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var decoderResult = new DecodePayloadResult("1")
            {
                CloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                    MessageId = "123",
                    Payload = "12",
                    DevEUI = c2dDevEUI is { } someDevEui ? DevEui.Parse(someDevEui) : null
                },
            };

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>(MockBehavior.Strict);
            payloadDecoder.Setup(x => x.DecodeMessageAsync(simulatedDevice.DevEUI, It.IsNotNull<byte[]>(), TestPort, It.IsAny<string>()))
                .ReturnsAsync(decoderResult);
            PayloadDecoder.SetDecoder(payloadDecoder.Object);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = CreateWaitableRequest(payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            payloadDataDown.Serialize(loraDevice.AppSKey.Value);
            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.Fcnt);

            // 6. Frame count has pending changes
            Assert.True(loraDevice.HasFrameCountChanges);

            LoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.VerifyAll();

            payloadDecoder.VerifyAll();
        }

        [Fact]
        public async Task When_Takes_Too_Long_Processing_C2D_Should_Abandon_Message()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var devAddr = simulatedDevice.DevAddr.Value;
            var devEUI = simulatedDevice.DevEUI;

            // Will get twin to initialize LoRaDevice
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(simulatedDevice.GetDefaultAbpTwin());
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            using var cloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage() { Payload = "c2d", Fport = TestPort }
                .CreateMessage();

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage);

            LoRaDeviceClient.Setup(x => x.AbandonAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "adad").AsList()));

            using var cache = NewMemoryCache();
            await using var loRaDeviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            using var request = WaitableLoRaRequest.Create(TestUtils.GenerateTestRadioMetadata(), DownstreamMessageSender,
                                                           inTimeForC2DMessageCheck: true,
                                                           inTimeForAdditionalMessageCheck: false,
                                                           inTimeForDownlinkDelivery: false,
                                                           payload);
            request.SetRegion(this.DefaultRegion);

            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            // 2. No downstream message
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Empty(DownstreamMessageSender.DownlinkMessages);

            // 3. Device FcntDown did change
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            Assert.Equal(InitialDeviceFcntDown + NetworkServer.Constants.MaxFcntUnsavedDelta, loRaDevice.FCntDown);
        }
    }
}
