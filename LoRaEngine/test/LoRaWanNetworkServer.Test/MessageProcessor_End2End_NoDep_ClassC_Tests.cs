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
        [InlineData(null, 0, 0)]
        [InlineData(null, 0, 9)]
        [InlineData(null, 5, 5)]
        [InlineData(null, 5, 14)]
        [InlineData(ServerGatewayID, 0, 0)]
        [InlineData(ServerGatewayID, 0, 19)]
        [InlineData(ServerGatewayID, 5, 5)]
        [InlineData(ServerGatewayID, 5, 24)]
        public async Task When_ABP_Sends_Upstream_And_DirectMethod_Should_Send_Upstream_And_Downstream(string deviceGatewayID, uint lastSavedFcntDown, uint currentFcntDown)
        {
            const uint payloadFcnt = 2; // to avoid relax mode reset
            var shouldSaveFcnt = (currentFcntDown - lastSavedFcntDown) + 1 >= Constants.MAX_FCNT_UNSAVED_DELTA;

            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID, deviceClassType: 'c'), frmCntDown: lastSavedFcntDown);

            if (shouldSaveFcnt)
            {
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .ReturnsAsync(true);
            }

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateABPTwin());

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(simDevice.CreateUnconfirmedMessageUplink("1", fcnt: payloadFcnt).Rxpk[0]);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // set the device fcnt down to the current, thus simulating multiple downstream calls
            Assert.True(deviceRegistry.InternalGetCachedDevicesForDevAddr(simDevice.DevAddr).TryGetValue(simDevice.DevEUI, out var loRaDevice));
            loRaDevice.SetFcntDown(currentFcntDown);

            var classCSender = new DefaultClassCDevicesMessageSender(
                this.ServerConfiguration,
                RegionFactory.CreateEU868Region(),
                deviceRegistry,
                this.PacketForwarder,
                this.FrameCounterUpdateStrategyProvider);

            var c2d = new LoRaCloudToDeviceMessage()
            {
                DevEUI = simDevice.DevEUI,
                MessageId = Guid.NewGuid().ToString(),
                Payload = "aaaa",
                Fport = 18,
            };

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                this.LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(simDevice.DevEUI, currentFcntDown, 0, this.ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)(currentFcntDown + 1));
            }

            Assert.True(await classCSender.SendAsync(c2d));
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downstreamMsg = this.PacketForwarder.DownlinkMessages[0];

            byte[] downstreamPayloadBytes = Convert.FromBase64String(downstreamMsg.Txpk.Data);
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(currentFcntDown + 1, downstreamPayload.GetFcnt());
            Assert.Equal(c2d.Fport, downstreamPayload.GetFPort());
            Assert.Equal(downstreamPayload.DevAddr.ToArray(), ConversionHelper.StringToByteArray(simDevice.DevAddr));
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey);
            Assert.Equal(c2d.Payload, Encoding.UTF8.GetString(decryptedPayload));

            Assert.Equal(currentFcntDown + 1, loRaDevice.FCntDown);
            Assert.Equal(payloadFcnt, loRaDevice.FCntUp);
            Assert.NotEqual(shouldSaveFcnt, loRaDevice.HasFrameCountChanges);

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
                RegionFactory.CreateEU868Region(),
                deviceRegistry,
                this.PacketForwarder,
                this.FrameCounterUpdateStrategyProvider);

            var c2d = new LoRaCloudToDeviceMessage()
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
            const uint payloadFcnt = 10;
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
                CloudToDeviceMessage = new NetworkServer.LoRaCloudToDeviceMessage()
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
            classCMessageSender.Setup(x => x.SendAsync(It.IsNotNull<ILoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Callback<ILoRaCloudToDeviceMessage, CancellationToken>((m, _) =>
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

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt);
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
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is unchanged
            Assert.Equal(InitialDeviceFcntDown, loraDevice.FCntDown);

            // 6. Frame count has pending changes
            Assert.True(loraDevice.HasFrameCountChanges);

            // Ensure the message was sent
            Assert.True(await c2dMessageSent.WaitAsync(10 * 1000));

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();

            payloadDecoder.VerifyAll();
            classCMessageSender.VerifyAll();
        }
    }
}