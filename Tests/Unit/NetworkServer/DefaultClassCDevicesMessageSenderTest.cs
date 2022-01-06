// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.LoRaPhysical;
    using global::LoRaTools.Regions;
    using global::LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class DefaultClassCDevicesMessageSenderTest : IDisposable
    {
        private const string ServerGatewayID = "test-gateway";
        private const FramePort TestPort = FramePorts.App10;

        private readonly NetworkServerConfiguration serverConfiguration;
        private readonly Region loRaRegion;
        private readonly LoRaDeviceRegistry loRaDeviceRegistry;
        private readonly Mock<IPacketForwarder> packetForwarder;
        private readonly Mock<LoRaDeviceAPIServiceBase> deviceApi;
        private readonly Mock<ILoRaDeviceClient> deviceClient;
        private readonly TestLoRaDeviceFactory loRaDeviceFactory;
        private readonly LoRaDeviceCache deviceCache = LoRaDeviceCacheDefault.CreateDefault();
        private readonly IMemoryCache cache;
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
            this.deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);

            this.cache = new MemoryCache(new MemoryCacheOptions());
#pragma warning disable CA2000 // Dispose objects before losing scope
            this.loRaDeviceFactory = new TestLoRaDeviceFactory(this.deviceClient.Object, this.deviceCache, new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance));
#pragma warning restore CA2000 // Dispose objects before losing scope
            this.loRaDeviceRegistry = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, this.deviceApi.Object, this.loRaDeviceFactory, this.deviceCache);
            this.frameCounterStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(this.serverConfiguration, this.deviceApi.Object);
        }

        private static void EnsureDownlinkIsCorrect(DownlinkMessage downlink, SimulatedDevice simDevice, ReceivedLoRaCloudToDeviceMessage sentMessage)
        {
            Assert.NotNull(downlink);
            Assert.NotEmpty(downlink.Data);

            var downstreamPayloadBytes = downlink.Data;
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(sentMessage.Fport, downstreamPayload.Fport);
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

            this.deviceApi.Setup(x => x.SearchByEuiAsync(DevEui.Parse(devEUI)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            var twin = simDevice.CreateABPTwin(reportedProperties: new Dictionary<string, object>
                {
                    { TwinProperty.Region, LoRaRegionType.EU868.ToString() },
                    { TwinProperty.LastProcessingStationEui, new StationEui(ulong.MaxValue).ToString() }
                });

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
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
                Fport = TestPort,
                MessageId = Guid.NewGuid().ToString(),
            };

            this.packetForwarder.Setup(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()))
                .Returns(Task.CompletedTask)
                .Callback<DownlinkMessage>(d =>
                {
                    EnsureDownlinkIsCorrect(d, simDevice, c2dToDeviceMessage);
                });

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.True(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.Verify(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()), Times.Once());

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Not_Class_C_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.SearchByEuiAsync(DevEui.Parse(devEUI)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simDevice.CreateABPTwin());

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = TestPort,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

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
                Fport = TestPort,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

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

            this.deviceApi.Setup(x => x.SearchByEuiAsync(DevEui.Parse(devEUI)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simDevice.CreateOTAATwin());

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = TestPort,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

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

            this.deviceApi.Setup(x => x.SearchByEuiAsync(DevEui.Parse(devEUI)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            var twin = simDevice.CreateABPTwin(reportedProperties: new Dictionary<string, object>
                {
                    { TwinProperty.Region, LoRaRegionType.EU868.ToString() },
                    { TwinProperty.LastProcessingStationEui, new StationEui(ulong.MaxValue).ToString() }
                });

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = TestPort,
                MessageId = Guid.NewGuid().ToString(),
            };

            // will update the fcnt down
            this.deviceApi.Setup(x => x.NextFCntDownAsync(devEUI, simDevice.FrmCntDown, 0, this.serverConfiguration.GatewayID))
                .ThrowsAsync(new TimeoutException());

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            _ = await Assert.ThrowsAsync<TimeoutException>(() => target.SendAsync(c2dToDeviceMessage));

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
                Fport = FramePort.MacCommand,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

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
                Fport = FramePort.MacCommand,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Has_Custom_RX2DR_Should_Send_Correctly()
        {
            const string devAddr = "023637F8";
            const string appSKey = "ABC02000000000000000000000000009ABC02000000000000000000000000009";
            const string nwkSKey = "ABC02000000000000000000000000009ABC02000000000000000000000000009";
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: 'c', gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;
            simDevice.SetupJoin(appSKey, nwkSKey, devAddr);

            this.deviceApi.Setup(x => x.SearchByEuiAsync(DevEui.Parse(devEUI)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            var twin = simDevice.CreateOTAATwin(
                desiredProperties: new Dictionary<string, object>
                {
                    { TwinProperty.RX2DataRate, "10" }
                },
                reportedProperties: new Dictionary<string, object>
                {
                    { TwinProperty.RX2DataRate, 10 },
                    { TwinProperty.Region, LoRaRegionType.US915.ToString() },
                    // OTAA device, already joined
                    { TwinProperty.DevAddr, devAddr },
                    { TwinProperty.AppSKey, appSKey },
                    { TwinProperty.NwkSKey, nwkSKey },
                    { TwinProperty.LastProcessingStationEui, new StationEui(ulong.MaxValue).ToString() }
                });

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = TestPort,
                MessageId = Guid.NewGuid().ToString(),
            };

            this.packetForwarder.Setup(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()))
                .Returns(Task.CompletedTask)
                .Callback<DownlinkMessage>(d =>
                {
                    EnsureDownlinkIsCorrect(d, simDevice, c2dToDeviceMessage);
                    Assert.Equal(DataRateIndex.DR10, d.DataRateRx2);
                    Assert.Equal(Hertz.Mega(923.3), d.FrequencyRx2);
                });

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.True(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.Verify(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()), Times.Once());

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_StationEui_Missing_Should_Fail()
        {
            const string devAddr = "023637F8";
            const string appSKey = "ABC02000000000000000000000000009ABC02000000000000000000000000009";
            const string nwkSKey = "ABC02000000000000000000000000009ABC02000000000000000000000000009";
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: 'c', gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;
            simDevice.SetupJoin(appSKey, nwkSKey, devAddr);

            this.deviceApi.Setup(x => x.SearchByEuiAsync(DevEui.Parse(devEUI)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            var twin = simDevice.CreateOTAATwin(
                desiredProperties: new Dictionary<string, object>
                {
                    { TwinProperty.RX2DataRate, "10" }
                },
                reportedProperties: new Dictionary<string, object>
                {
                    { TwinProperty.RX2DataRate, 10 },
                    { TwinProperty.Region, LoRaRegionType.US915.ToString() },
                    // OTAA device, already joined
                    { TwinProperty.DevAddr, devAddr },
                    { TwinProperty.AppSKey, appSKey },
                    { TwinProperty.NwkSKey, nwkSKey },
                });

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = TestPort,
                MessageId = Guid.NewGuid().ToString(),
            };

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.packetForwarder.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.packetForwarder.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        public void Dispose()
        {
            this.loRaDeviceRegistry.Dispose();
            this.cache.Dispose();
            this.deviceCache.Dispose();
        }
    }
}