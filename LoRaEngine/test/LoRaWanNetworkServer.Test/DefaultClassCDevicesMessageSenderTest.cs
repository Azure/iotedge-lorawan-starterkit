// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class DefaultClassCDevicesMessageSenderTest
    {
        const string ServerGatewayID = "test-gateway";

        private readonly NetworkServerConfiguration serverConfiguration;
        private readonly Region loRaRegion;
        private readonly LoRaDeviceRegistry loRaDeviceRegistry;
        private readonly Mock<IPacketForwarder> packetForwarder;
        private readonly Mock<LoRaDeviceAPIServiceBase> deviceApi;
        private readonly Mock<ILoRaDeviceClient> deviceClient;
        private readonly TestLoRaDeviceFactory loRaDeviceFactory;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterStrategyProvider;

        public DefaultClassCDevicesMessageSenderTest()
        {
            this.serverConfiguration = new NetworkServerConfiguration()
            {
                GatewayID = ServerGatewayID,
            };

            this.loRaRegion = RegionManager.EU868;

            this.packetForwarder = new Mock<IPacketForwarder>(MockBehavior.Strict);
            this.deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.loRaDeviceFactory = new TestLoRaDeviceFactory(this.deviceClient.Object);
            this.loRaDeviceRegistry = new LoRaDeviceRegistry(this.serverConfiguration, new MemoryCache(new MemoryCacheOptions()), this.deviceApi.Object, this.loRaDeviceFactory);
            this.frameCounterStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(this.serverConfiguration.GatewayID, this.deviceApi.Object);
        }

        private void EnsureDownlinkIsCorrect(DownlinkPktFwdMessage downlink, SimulatedDevice simDevice, ReceivedLoRaCloudToDeviceMessage sentMessage)
        {
            Assert.NotNull(downlink);
            Assert.NotNull(downlink.Txpk);
            Assert.True(downlink.Txpk.Imme);
            Assert.Equal(0, downlink.Txpk.Tmst);
            Assert.NotEmpty(downlink.Txpk.Data);

            byte[] downstreamPayloadBytes = Convert.FromBase64String(downlink.Txpk.Data);
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(sentMessage.Fport, downstreamPayload.GetFPort());
            Assert.Equal(downstreamPayload.DevAddr.ToArray(), ConversionHelper.StringToByteArray(simDevice.DevAddr));
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey);
            Assert.Equal(sentMessage.Payload, Encoding.UTF8.GetString(decryptedPayload));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(ServerGatewayID)]
        public async Task When_Sending_Message_Should_Send_Downlink_To_PacketForwarder(string deviceGatewayID)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: 'c', gatewayID: deviceGatewayID));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.SearchByDevEUIAsync(devEUI))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            var twin = simDevice.CreateABPTwin(reportedProperties: new Dictionary<string, object>
                {
                    { TwinProperty.Region, LoRaRegionType.EU868.ToString() }
                });

            this.deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                // will update the fcnt down
                this.deviceApi.Setup(x => x.NextFCntDownAsync(devEUI, simDevice.FrmCntDown, 0, this.serverConfiguration.GatewayID))
                    .ReturnsAsync((ushort)(simDevice.FrmCntDown + 1));
            }

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = 10,
                MessageId = Guid.NewGuid().ToString(),
            };

            this.packetForwarder.Setup(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkPktFwdMessage>()))
                .Returns(Task.CompletedTask)
                .Callback<DownlinkPktFwdMessage>(d =>
                {
                    this.EnsureDownlinkIsCorrect(d, simDevice, c2dToDeviceMessage);
                });

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider);

            Assert.True(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.Verify(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkPktFwdMessage>()), Times.Once());

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Not_Class_C_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.SearchByDevEUIAsync(devEUI))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            this.deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateABPTwin());

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = 10,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Fport_Is_Not_Set_Should_Fail()
        {
            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                Fport = 10,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Not_Joined_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: 'c', gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.SearchByDevEUIAsync(devEUI))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            this.deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateOTAATwin());

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = 10,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Fails_To_Get_FcntDown_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: 'c'));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.SearchByDevEUIAsync(devEUI))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            var twin = simDevice.CreateABPTwin(reportedProperties: new Dictionary<string, object>
                {
                    { TwinProperty.Region, LoRaRegionType.EU868.ToString() }
                });

            this.deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = 10,
                MessageId = Guid.NewGuid().ToString(),
            };

            // will update the fcnt down
            this.deviceApi.Setup(x => x.NextFCntDownAsync(devEUI, simDevice.FrmCntDown, 0, this.serverConfiguration.GatewayID))
                .ThrowsAsync(new TimeoutException());

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Invalid_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: 'c', gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = LoRaFPort.MacCommand,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Regions_Is_Not_Defined_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: 'c', gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = LoRaFPort.MacCommand,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }
    }
}
