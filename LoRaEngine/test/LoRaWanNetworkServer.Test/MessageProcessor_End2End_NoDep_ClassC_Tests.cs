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
    using LoRaTools.CommonAPI;
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
    // Class C device tests
    public class MessageProcessor_End2End_NoDep_ClassC_Tests : MessageProcessorTestBase
    {
        public MessageProcessor_End2End_NoDep_ClassC_Tests()
        {
        }

        [Theory]
        [InlineData(null, 0U, 0U)]
        [InlineData(null, 0U, 9U)]
        [InlineData(null, 5U, 5U)]
        [InlineData(null, 5U, 14U)]
        [InlineData(ServerGatewayID, 0U, 0U)]
        [InlineData(ServerGatewayID, 0U, 19U)]
        [InlineData(ServerGatewayID, 5U, 5U)]
        [InlineData(ServerGatewayID, 5U, 24U)]
        public async Task When_ABP_Sends_Upstream_Followed_By_DirectMethod_Should_Send_Upstream_And_Downstream(string deviceGatewayID, uint fcntDownFromTwin, uint fcntDelta)
        {
            const uint payloadFcnt = 2; // to avoid relax mode reset

            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID, deviceClassType: 'c'), frmCntDown: fcntDownFromTwin);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var twin = simDevice.CreateABPTwin(reportedProperties: new Dictionary<string, object>
                {
                    { TwinProperty.Region, LoRaRegionType.EU868.ToString() }
                });

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            if (deviceGatewayID == null)
            {
                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                    .ReturnsAsync(new FunctionBundlerResult());
            }

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(simDevice.CreateUnconfirmedMessageUplink("1", fcnt: payloadFcnt).Rxpk[0]);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // wait until cache has been updated
            await Task.Delay(50);

            // Adds fcntdown to device, simulating multiple downstream calls
            Assert.True(deviceRegistry.InternalGetCachedDevicesForDevAddr(simDevice.DevAddr).TryGetValue(simDevice.DevEUI, out var loRaDevice));
            loRaDevice.SetFcntDown(fcntDelta + loRaDevice.FCntDown);

            var classCSender = new DefaultClassCDevicesMessageSender(
                this.ServerConfiguration,
                deviceRegistry,
                this.PacketForwarder,
                this.FrameCounterUpdateStrategyProvider);

            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = simDevice.DevEUI,
                MessageId = Guid.NewGuid().ToString(),
                Payload = "aaaa",
                Fport = 18,
            };

            var expectedFcntDown = fcntDownFromTwin + Constants.MAX_FCNT_UNSAVED_DELTA + fcntDelta;

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(simDevice.DevEUI, fcntDownFromTwin + fcntDelta, 0, this.ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)expectedFcntDown);
            }

            Assert.True(await classCSender.SendAsync(c2d));
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downstreamMsg = this.PacketForwarder.DownlinkMessages[0];

            byte[] downstreamPayloadBytes = Convert.FromBase64String(downstreamMsg.Txpk.Data);
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(expectedFcntDown, downstreamPayload.GetFcnt());
            Assert.Equal(c2d.Fport, downstreamPayload.GetFPort());
            Assert.Equal(downstreamPayload.DevAddr.ToArray(), ConversionHelper.StringToByteArray(simDevice.DevAddr));
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey);
            Assert.Equal(c2d.Payload, Encoding.UTF8.GetString(decryptedPayload));

            Assert.Equal(expectedFcntDown, loRaDevice.FCntDown);
            Assert.Equal(payloadFcnt, loRaDevice.FCntUp);

            Assert.False(loRaDevice.HasFrameCountChanges);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(ServerGatewayID)]
        public async Task When_OTAA_Join_Then_Sends_Upstream_DirectMethod_Should_Send_Downstream(string deviceGatewayID)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: 'c', gatewayID: deviceGatewayID));

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateOTAATwin());

            var savedAppSKey = string.Empty;
            var savedNwkSKey = string.Empty;
            var savedDevAddr = string.Empty;
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection>((t) =>
                {
                    savedAppSKey = t[TwinProperty.AppSKey];
                    savedNwkSKey = t[TwinProperty.NwkSKey];
                    savedDevAddr = t[TwinProperty.DevAddr];

                    Assert.NotEmpty(savedAppSKey);
                    Assert.NotEmpty(savedNwkSKey);
                    Assert.NotEmpty(savedDevAddr);
                });

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            if (deviceGatewayID == null)
            {
                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                    .ReturnsAsync(new FunctionBundlerResult());
            }

            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, simDevice.DevEUI, simDevice.AppEUI, It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var joinRxpk = simDevice.CreateJoinRequest().SerializeUplink(simDevice.AppKey).Rxpk[0];
            var joinRequest = this.CreateWaitableRequest(joinRxpk);
            messageDispatcher.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded);

            simDevice.SetupJoin(savedAppSKey, savedNwkSKey, savedDevAddr);
            var request = this.CreateWaitableRequest(simDevice.CreateUnconfirmedMessageUplink("1").Rxpk[0]);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            var classCSender = new DefaultClassCDevicesMessageSender(
                this.ServerConfiguration,
                deviceRegistry,
                this.PacketForwarder,
                this.FrameCounterUpdateStrategyProvider);

            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = simDevice.DevEUI,
                MessageId = Guid.NewGuid().ToString(),
                Payload = "aaaa",
                Fport = 14,
            };

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(simDevice.DevEUI, simDevice.FrmCntDown, 0, this.ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)(simDevice.FrmCntDown + 1));
            }

            Assert.True(await classCSender.SendAsync(c2d));
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            var downstreamMsg = this.PacketForwarder.DownlinkMessages[1];

            TestLogger.Log($"appSKey: {simDevice.AppSKey}, nwkSKey: {simDevice.NwkSKey}");

            byte[] downstreamPayloadBytes = Convert.FromBase64String(downstreamMsg.Txpk.Data);
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(1, downstreamPayload.GetFcnt());
            Assert.Equal(c2d.Fport, downstreamPayload.GetFPort());
            Assert.Equal(downstreamPayload.DevAddr.ToArray(), ConversionHelper.StringToByteArray(savedDevAddr));
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey);
            Assert.Equal(c2d.Payload, Encoding.UTF8.GetString(decryptedPayload));

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        [Fact]
        public async Task Unconfirmed_Cloud_To_Device_From_Decoder_Should_Call_ClassC_Message_Sender()
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

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var decoderResult = new DecodePayloadResult("1")
            {
                CloudToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
                {
                    Fport = 1,
                    MessageId = "123",
                    Payload = "12",
                    DevEUI = "0000000000000002",
                },
            };

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>(MockBehavior.Strict);
            payloadDecoder.Setup(x => x.DecodeMessageAsync(simulatedDevice.DevEUI, It.IsNotNull<byte[]>(), 1, It.IsAny<string>()))
                .ReturnsAsync(decoderResult);
            this.PayloadDecoder.SetDecoder(payloadDecoder.Object);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var c2dMessageSent = new SemaphoreSlim(0);
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.IsNotNull<IReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Callback<IReceivedLoRaCloudToDeviceMessage, CancellationToken>((m, _) =>
                {
                    Assert.False(m.Confirmed);
                    Assert.Equal("0000000000000002", m.DevEUI);
                    c2dMessageSent.Release();
                });
            this.RequestHandlerImplementation.SetClassCMessageSender(classCMessageSender.Object);

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

            // 2. No downstream message for the current device is sent
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is unchanged
            Assert.Equal(InitialDeviceFcntDown, loraDevice.FCntDown);

            // 6. Frame count has pending changes
            Assert.True(loraDevice.HasFrameCountChanges);

            // Ensure the message was sent
            Assert.True(await c2dMessageSent.WaitAsync(10 * 1000));

            payloadDecoder.VerifyAll();
            classCMessageSender.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task When_Joining_Should_Save_Region_And_Preferred_Gateway(
            [CombinatorialValues(null, ServerGatewayID)] string deviceGatewayID,
            [CombinatorialValues(null, ServerGatewayID, "another-gateway")] string initialPreferredGatewayID,
            [CombinatorialValues(null, LoRaRegionType.EU868, LoRaRegionType.US915)] LoRaRegionType? initialLoRaRegion)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: 'c', gatewayID: deviceGatewayID));

            var customReportedProperties = new Dictionary<string, object>();
            // reported: { 'PreferredGateway': '' } -> if device is for multiple gateways and one initial was defined
            if (string.IsNullOrEmpty(deviceGatewayID) && !string.IsNullOrEmpty(initialPreferredGatewayID))
                customReportedProperties[TwinProperty.PreferredGatewayID] = initialPreferredGatewayID;

            if (initialLoRaRegion.HasValue)
                customReportedProperties[TwinProperty.Region] = initialLoRaRegion.Value.ToString();

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateOTAATwin(reportedProperties: customReportedProperties));

            var shouldSavePreferredGateway = string.IsNullOrEmpty(deviceGatewayID) && initialPreferredGatewayID != ServerGatewayID;
            var shouldSaveRegion = !initialLoRaRegion.HasValue || initialLoRaRegion.Value != LoRaRegionType.EU868;

            var savedAppSKey = string.Empty;
            var savedNwkSKey = string.Empty;
            var savedDevAddr = string.Empty;
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true)
                .Callback<TwinCollection>((t) =>
                {
                    savedAppSKey = t[TwinProperty.AppSKey];
                    savedNwkSKey = t[TwinProperty.NwkSKey];
                    savedDevAddr = t[TwinProperty.DevAddr];

                    Assert.NotEmpty(savedAppSKey);
                    Assert.NotEmpty(savedNwkSKey);
                    Assert.NotEmpty(savedDevAddr);

                    if (shouldSaveRegion)
                        Assert.Equal(LoRaRegionType.EU868.ToString(), t[TwinProperty.Region].Value as string);
                    else
                        Assert.False(t.Contains(TwinProperty.Region));

                    // Only save preferred gateway if device does not have one assigned
                    if (shouldSavePreferredGateway)
                        Assert.Equal(this.ServerConfiguration.GatewayID, t[TwinProperty.PreferredGatewayID].Value as string);
                    else
                        Assert.False(t.Contains(TwinProperty.PreferredGatewayID));
                });

            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, simDevice.DevEUI, simDevice.AppEUI, It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var joinRxpk = simDevice.CreateJoinRequest().SerializeUplink(simDevice.AppKey).Rxpk[0];
            var joinRequest = this.CreateWaitableRequest(joinRxpk);
            messageDispatcher.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded);

            var devices = deviceRegistry.InternalGetCachedDevicesForDevAddr(savedDevAddr);
            Assert.True(devices.TryGetValue(simDevice.DevEUI, out var loRaDevice));

            Assert.Equal(LoRaDeviceClassType.C, loRaDevice.ClassType);
            if (string.IsNullOrEmpty(simDevice.LoRaDevice.GatewayID))
                Assert.Equal(this.ServerConfiguration.GatewayID, loRaDevice.PreferredGatewayID);
            else
                Assert.Empty(loRaDevice.PreferredGatewayID);

            Assert.Equal(LoRaRegionType.EU868, loRaDevice.LoRaRegion);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task When_Processing_Data_Request_Should_Compute_Preferred_Gateway_And_Region(
            [CombinatorialValues(null, ServerGatewayID)] string deviceGatewayID,
            [CombinatorialValues(null, ServerGatewayID, "another-gateway")] string initialPreferredGatewayID,
            [CombinatorialValues(ServerGatewayID, "another-gateway")] string preferredGatewayID,
            [CombinatorialValues(null, LoRaRegionType.EU868, LoRaRegionType.US915)] LoRaRegionType? initialLoRaRegion)
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID, deviceClassType: 'c'),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);
            loraDevice.UpdatePreferredGatewayID(initialPreferredGatewayID, acceptChanges: true);
            if (initialLoRaRegion.HasValue)
                loraDevice.UpdateRegion(initialLoRaRegion.Value, acceptChanges: true);

            var shouldSavePreferredGateway = string.IsNullOrEmpty(deviceGatewayID) && initialPreferredGatewayID != preferredGatewayID && preferredGatewayID == ServerGatewayID;
            var shouldSaveRegion = (!initialLoRaRegion.HasValue || initialLoRaRegion.Value != LoRaRegionType.EU868) && (preferredGatewayID == ServerGatewayID || deviceGatewayID != null);

            var bundlerResult = new FunctionBundlerResult()
            {
                PreferredGatewayResult = new PreferredGatewayResult()
                {
                    DevEUI = simulatedDevice.DevEUI,
                    PreferredGatewayID = preferredGatewayID,
                    CurrentFcntUp = PayloadFcnt,
                    RequestFcntUp = PayloadFcnt,
                }
            };

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simulatedDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                    .Callback<string, FunctionBundlerRequest>((devEUI, bundlerRequest) =>
                    {
                        Assert.Equal(PayloadFcnt, bundlerRequest.ClientFCntUp);
                        Assert.Equal(ServerGatewayID, bundlerRequest.GatewayId);
                        Assert.Equal(FunctionBundlerItemType.PreferredGateway, bundlerRequest.FunctionItems);
                    })
                    .ReturnsAsync(bundlerResult);
            }

            if (shouldSavePreferredGateway || shouldSaveRegion)
            {
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .Callback<TwinCollection>((savedTwin) =>
                    {
                        if (shouldSavePreferredGateway)
                            Assert.Equal(ServerGatewayID, savedTwin[TwinProperty.PreferredGatewayID].Value as string);
                        else
                            Assert.False(savedTwin.Contains(TwinProperty.PreferredGatewayID));

                        if (shouldSaveRegion)
                            Assert.Equal(LoRaRegionType.EU868.ToString(), savedTwin[TwinProperty.Region].Value as string);
                        else
                            Assert.False(savedTwin.Contains(TwinProperty.Region));
                    })
                    .ReturnsAsync(true);
            }

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

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

            // 2. No downstream message for the current device is sent
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            if (!string.IsNullOrEmpty(deviceGatewayID))
                Assert.Equal(initialPreferredGatewayID, loraDevice.PreferredGatewayID);
            else
                Assert.Equal(preferredGatewayID, loraDevice.PreferredGatewayID);

            Assert.Equal(LoRaRegionType.EU868, loraDevice.LoRaRegion);
        }

        [Fact]
        public async Task When_Updating_PreferredGateway_And_FcntUp_Should_Save_Twin_Once()
        {
            const uint PayloadFcnt = 10;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, deviceClassType: 'c'),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);
            loraDevice.UpdatePreferredGatewayID("another-gateway", acceptChanges: true);

            var bundlerResult = new FunctionBundlerResult()
            {
                PreferredGatewayResult = new PreferredGatewayResult()
                {
                    DevEUI = simulatedDevice.DevEUI,
                    PreferredGatewayID = ServerGatewayID,
                    CurrentFcntUp = PayloadFcnt,
                    RequestFcntUp = PayloadFcnt,
                }
            };

            this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simulatedDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                .Callback<string, FunctionBundlerRequest>((devEUI, bundlerRequest) =>
                {
                    Assert.Equal(PayloadFcnt, bundlerRequest.ClientFCntUp);
                    Assert.Equal(ServerGatewayID, bundlerRequest.GatewayId);
                    Assert.Equal(FunctionBundlerItemType.PreferredGateway, bundlerRequest.FunctionItems);
                })
                .ReturnsAsync(bundlerResult);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((savedTwin) =>
                {
                    Assert.Equal(ServerGatewayID, savedTwin[TwinProperty.PreferredGatewayID].Value as string);
                    Assert.Equal(LoRaRegionType.EU868.ToString(), savedTwin[TwinProperty.Region].Value as string);
                    Assert.Equal(PayloadFcnt, (uint)savedTwin[TwinProperty.FCntUp].Value);
                })
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

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

            // 2. No downstream message for the current device is sent
            Assert.Null(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);

            Assert.Equal(ServerGatewayID, loraDevice.PreferredGatewayID);
            Assert.Equal(LoRaRegionType.EU868, loraDevice.LoRaRegion);
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            this.LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()), Times.Once());
        }
    }
}