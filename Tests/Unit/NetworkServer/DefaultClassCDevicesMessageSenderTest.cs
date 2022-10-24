// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.LoRaPhysical;
    using global::LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class DefaultClassCDevicesMessageSenderTest : IAsyncDisposable
    {
        private const string ServerGatewayID = "test-gateway";
        private const FramePort TestPort = FramePorts.App10;

        private readonly NetworkServerConfiguration serverConfiguration;
        private readonly Region loRaRegion;
        private readonly LoRaDeviceRegistry loRaDeviceRegistry;
        private readonly Mock<IDownstreamMessageSender> downstreamMessageSender;
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

            this.downstreamMessageSender = new Mock<IDownstreamMessageSender>(MockBehavior.Strict);
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
            Assert.False(downlink.Data.IsEmpty);

            var downstreamPayloadBytes = downlink.Data;
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(sentMessage.Fport, downstreamPayload.Fport);
            Assert.Equal(downstreamPayload.DevAddr, simDevice.DevAddr);
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey.Value);
            Assert.Equal(sentMessage.Payload, Encoding.UTF8.GetString(decryptedPayload));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(ServerGatewayID)]
        public async Task When_Sending_Message_Should_Send_Downlink_To_DownstreamMessageSender(string deviceGatewayID)
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: deviceGatewayID));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            var twin = LoRaDeviceTwin.Create(simDevice.LoRaDevice.GetAbpDesiredTwinProperties(),
                                             simDevice.GetAbpReportedTwinProperties() with
                                             {
                                                 Region = LoRaRegionType.EU868,
                                                 LastProcessingStation = new StationEui(ulong.MaxValue)
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

            DownlinkMessage receivedDownlinkMessage = null;
            this.downstreamMessageSender.Setup(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()))
                .Returns(Task.CompletedTask)
                .Callback<DownlinkMessage>(d => receivedDownlinkMessage = d);

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.True(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.Verify(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()), Times.Once());
            EnsureDownlinkIsCorrect(receivedDownlinkMessage, simDevice, c2dToDeviceMessage);

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Not_Class_C_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simDevice.GetDefaultAbpTwin());

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
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.VerifyAll();
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
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Not_Joined_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(LoRaDeviceTwin.Create(simDevice.LoRaDevice.GetOtaaDesiredTwinProperties(), simDevice.GetOtaaReportedTwinProperties()));

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
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Fails_To_Get_FcntDown_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: LoRaDeviceClassType.C));
            var devEUI = simDevice.DevEUI;

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            var twin = LoRaDeviceTwin.Create(simDevice.LoRaDevice.GetAbpDesiredTwinProperties(),
                                             simDevice.GetAbpReportedTwinProperties() with
                                             {
                                                 Region = LoRaRegionType.EU868,
                                                 LastProcessingStation = new StationEui(ulong.MaxValue)
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
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            _ = await Assert.ThrowsAsync<TimeoutException>(() => target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Invalid_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: ServerGatewayID));
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
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Regions_Is_Not_Defined_Should_Fail()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: ServerGatewayID));
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
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_Has_Custom_RX2DR_Should_Send_Correctly()
        {
            var devAddr = new DevAddr(0x023637F8);
            var appSKey = TestKeys.CreateAppSessionKey(0xABC0200000000000, 0x09);
            var nwkSKey = TestKeys.CreateNetworkSessionKey(0xABC0200000000000, 0x09);
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;
            simDevice.SetupJoin(appSKey, nwkSKey, devAddr);

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            var twin = LoRaDeviceTwin.Create(
                simDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
                {
                    Rx2DataRate = DataRateIndex.DR10
                },
                simDevice.GetOtaaReportedTwinProperties() with
                {
                    Rx2DataRate = DataRateIndex.DR10,
                    Region = LoRaRegionType.US915,
                    // OTAA device, already joined
                    DevAddr = devAddr,
                    AppSessionKey = appSKey,
                    NetworkSessionKey = nwkSKey,
                    LastProcessingStation = new StationEui(ulong.MaxValue)
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

            DownlinkMessage receivedDownlinkMessage = null;
            this.downstreamMessageSender.Setup(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()))
                .Returns(Task.CompletedTask)
                .Callback<DownlinkMessage>(d => receivedDownlinkMessage = d);

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.True(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.Verify(x => x.SendDownstreamAsync(It.IsNotNull<DownlinkMessage>()), Times.Once());

            EnsureDownlinkIsCorrect(receivedDownlinkMessage, simDevice, c2dToDeviceMessage);
            Assert.Equal(DataRateIndex.DR10, receivedDownlinkMessage.Rx2.DataRate);
            Assert.Equal(Hertz.Mega(923.3), receivedDownlinkMessage.Rx2.Frequency);

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Fact]
        public async Task When_StationEui_Missing_Should_Fail()
        {
            var devAddr = new DevAddr(0x023637F8);
            var appSKey = TestKeys.CreateAppSessionKey(0xABC0200000000000, 0x09);
            var nwkSKey = TestKeys.CreateNetworkSessionKey(0xABC0200000000000, 0x09);
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, deviceClassType: LoRaDeviceClassType.C, gatewayID: ServerGatewayID));
            var devEUI = simDevice.DevEUI;
            simDevice.SetupJoin(appSKey, nwkSKey, devAddr);

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            var twin = LoRaDeviceTwin.Create(
                simDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
                {
                    Rx2DataRate = DataRateIndex.DR10
                },
                simDevice.GetOtaaReportedTwinProperties() with
                {
                    Rx2DataRate = DataRateIndex.DR10,
                    Region = LoRaRegionType.US915,
                    // OTAA device, already joined
                    DevAddr = devAddr,
                    AppSessionKey = appSKey,
                    NetworkSessionKey = nwkSKey
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
                this.downstreamMessageSender.Object,
                this.frameCounterStrategyProvider,
                NullLogger<DefaultClassCDevicesMessageSender>.Instance,
                TestMeter.Instance);

            Assert.False(await target.SendAsync(c2dToDeviceMessage));

            this.downstreamMessageSender.VerifyAll();
            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        public async ValueTask DisposeAsync()
        {
            await this.loRaDeviceRegistry.DisposeAsync();
            this.cache.Dispose();
            await this.deviceCache.DisposeAsync();
        }
    }
}
