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
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Class CCloud to device message processing max payload size tests (Join tests are handled in other class)
    [Collection(TestConstants.C2D_Size_Limit_TestCollectionName)]
    public sealed class ClassCCloudToDeviceMessageSizeLimitTests : IAsyncDisposable
    {
        private const string ServerGatewayID = "test-gateway";

        private TestDownstreamMessageSender DownstreamMessageSender { get; }

        private readonly NetworkServerConfiguration serverConfiguration;
        private readonly Region loRaRegion;
        private readonly Mock<LoRaDeviceAPIServiceBase> deviceApi;
        private readonly Mock<ILoRaDeviceClient> deviceClient;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;
        private readonly TestLoRaDeviceFactory loRaDeviceFactory;
        private readonly MemoryCache cache;
        private readonly LoRaDeviceClientConnectionManager connectionManager;
        private readonly LoRaDeviceRegistry loRaDeviceRegistry;
        private readonly LoRaDeviceCache deviceCache = LoRaDeviceCacheDefault.CreateDefault();
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterStrategyProvider;

        public ClassCCloudToDeviceMessageSizeLimitTests(ITestOutputHelper testOutputHelper)
        {
            this.serverConfiguration = new NetworkServerConfiguration()
            {
                GatewayID = ServerGatewayID,
            };

            this.loRaRegion = RegionManager.EU868;
            DownstreamMessageSender = new TestDownstreamMessageSender();
            this.deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);

            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.cache, this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<LoRaDeviceClientConnectionManager>());
            this.loRaDeviceFactory = new TestLoRaDeviceFactory(this.deviceClient.Object, this.deviceCache, this.connectionManager);

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
        [CombinatorialData]
        public async Task MessageProcessor_End2End_NoDep_ClassC_CloudToDeviceMessage_SizeLimit_Should_Accept(
            bool hasMacInC2D)
        {
            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(
                    1, deviceClassType: LoRaDeviceClassType.C, gatewayID: this.serverConfiguration.GatewayID));

            var devEUI = simulatedDevice.DevEUI;

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetAbpDesiredTwinProperties(),
                                             simulatedDevice.GetAbpReportedTwinProperties() with
                                             {
                                                 Region = LoRaRegionType.EU868,
                                                 LastProcessingStation = new StationEui(ulong.MaxValue)
                                             });
            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;

            var c2dPayloadSize = this.loRaRegion.GetMaxPayloadSize(this.loRaRegion.GetDefaultRX2ReceiveWindow(default).DataRate)
                - c2dMessageMacCommandSize
                - NetworkServer.Constants.LoraProtocolOverheadSize;

            var c2dMsgPayload = GeneratePayload("123457890", (int)c2dPayloadSize);
            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = devEUI,
                Payload = c2dMsgPayload,
                Fport = FramePorts.App1,
            };

            if (hasMacInC2D)
            {
                c2d.MacCommands.Add(c2dMessageMacCommand);
            }

            using var cloudToDeviceMessage = c2d.CreateMessage();

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                DownstreamMessageSender,
                this.frameCounterStrategyProvider,
                this.testOutputLoggerFactory.CreateLogger<DefaultClassCDevicesMessageSender>(),
                TestMeter.Instance);

            // Expectations
            // Verify that C2D message is sent
            Assert.True(await target.SendAsync(c2d));

            // Verify that exactly one C2D message was received
            Assert.Single(DownstreamMessageSender.DownlinkMessages);

            // Verify donwlink message is correct
            EnsureDownlinkIsCorrect(
                DownstreamMessageSender.DownlinkMessages.First(), simulatedDevice, c2d);

            // Get C2D message payload
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            payloadDataDown.Serialize(simulatedDevice.AppSKey.Value);

            // Verify that expected Mac commands are present
            var expectedMacCommandsCount = 0;

            if (hasMacInC2D)
                expectedMacCommandsCount++;

            if (expectedMacCommandsCount > 0)
            {
                var macCommands = MacCommand.CreateServerMacCommandFromBytes(payloadDataDown.Fopts);
                Assert.Equal(expectedMacCommandsCount, macCommands.Count);
            }
            else
            {
                Assert.Null(payloadDataDown.MacCommands);
            }

            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task MessageProcessor_End2End_NoDep_ClassC_CloudToDeviceMessage_SizeLimit_Should_Reject(
            bool hasMacInC2D)
        {
            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(
                    1, deviceClassType: LoRaDeviceClassType.C, gatewayID: this.serverConfiguration.GatewayID));

            var devEUI = simulatedDevice.DevEUI;

            this.deviceApi.Setup(x => x.GetPrimaryKeyByEuiAsync(devEUI))
                .ReturnsAsync("123");

            this.deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simulatedDevice.GetDefaultAbpTwin());

            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;

            var c2dPayloadSize = this.loRaRegion.GetMaxPayloadSize(this.loRaRegion.GetDefaultRX2ReceiveWindow(default).DataRate)
                - c2dMessageMacCommandSize
                + 1 // make message too long on purpose
                - NetworkServer.Constants.LoraProtocolOverheadSize;

            var c2dMsgPayload = GeneratePayload("123457890", (int)c2dPayloadSize);
            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = devEUI,
                Payload = c2dMsgPayload,
                Fport = FramePorts.App1,
            };

            if (hasMacInC2D)
            {
                c2d.MacCommands.Add(c2dMessageMacCommand);
            }

            using var cloudToDeviceMessage = c2d.CreateMessage();

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                DownstreamMessageSender,
                this.frameCounterStrategyProvider,
                this.testOutputLoggerFactory.CreateLogger<DefaultClassCDevicesMessageSender>(),
                TestMeter.Instance);

            // Expectations
            // Verify that C2D message is sent
            Assert.False(await target.SendAsync(c2d));

            // Verify that exactly one C2D message was received
            Assert.Empty(DownstreamMessageSender.DownlinkMessages);

            this.deviceApi.VerifyAll();
            this.deviceClient.VerifyAll();
        }

        private static string GeneratePayload(string allowedChars, int length)
        {
            var random = new Random();

            var chars = new char[length];
            var setLength = allowedChars.Length;

            for (var i = 0; i < length; ++i)
            {
                chars[i] = allowedChars[random.Next(setLength)];
            }

            return new string(chars, 0, length);
        }

        public async ValueTask DisposeAsync()
        {
            await this.loRaDeviceRegistry.DisposeAsync();
            await this.connectionManager.DisposeAsync();
            await this.deviceCache.DisposeAsync();

            this.cache.Dispose();
            this.testOutputLoggerFactory.Dispose();
        }
    }
}
