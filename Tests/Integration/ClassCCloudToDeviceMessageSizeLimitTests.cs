// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Class CCloud to device message processing max payload size tests (Join tests are handled in other class)
    [Collection(TestConstants.C2D_Size_Limit_TestCollectionName)]
    public sealed class ClassCCloudToDeviceMessageSizeLimitTests : IDisposable
    {
        private const string ServerGatewayID = "test-gateway";

        private TestPacketForwarder PacketForwarder { get; }

        private readonly NetworkServerConfiguration serverConfiguration;
        private readonly Region loRaRegion;
        private readonly Mock<LoRaDeviceAPIServiceBase> deviceApi;
        private readonly Mock<ILoRaDeviceClient> deviceClient;
        private readonly TestLoRaDeviceFactory loRaDeviceFactory;
        private readonly MemoryCache cache;
        private readonly LoRaDeviceRegistry loRaDeviceRegistry;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterStrategyProvider;

        public ClassCCloudToDeviceMessageSizeLimitTests()
        {
            this.serverConfiguration = new NetworkServerConfiguration()
            {
                GatewayID = ServerGatewayID,
            };

            this.loRaRegion = RegionManager.EU868;
            PacketForwarder = new TestPacketForwarder();
            this.deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.loRaDeviceFactory = new TestLoRaDeviceFactory(this.deviceClient.Object);
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.loRaDeviceRegistry = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, this.deviceApi.Object, this.loRaDeviceFactory);
            this.frameCounterStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(this.serverConfiguration, this.deviceApi.Object);
        }

        private static void EnsureDownlinkIsCorrect(DownlinkPktFwdMessage downlink, SimulatedDevice simDevice, ReceivedLoRaCloudToDeviceMessage sentMessage)
        {
            Assert.NotNull(downlink);
            Assert.NotNull(downlink.Txpk);
            Assert.True(downlink.Txpk.Imme);
            Assert.Equal(0, downlink.Txpk.Tmst);
            Assert.NotEmpty(downlink.Txpk.Data);

            var downstreamPayloadBytes = Convert.FromBase64String(downlink.Txpk.Data);
            var downstreamPayload = new LoRaPayloadData(downstreamPayloadBytes);
            Assert.Equal(sentMessage.Fport, downstreamPayload.FPortValue);
            Assert.Equal(downstreamPayload.DevAddr.ToArray(), ConversionHelper.StringToByteArray(simDevice.DevAddr));
            var decryptedPayload = downstreamPayload.GetDecryptedPayload(simDevice.AppSKey);
            Assert.Equal(sentMessage.Payload, Encoding.UTF8.GetString(decryptedPayload));
        }

        [Theory]
        [CombinatorialData]
        public async Task MessageProcessor_End2End_NoDep_ClassC_CloudToDeviceMessage_SizeLimit_Should_Accept(
            bool hasMacInC2D)
        {
            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(
                    1, deviceClassType: 'c', gatewayID: this.serverConfiguration.GatewayID));

            var devEUI = simulatedDevice.DevEUI;

            this.deviceApi.Setup(x => x.SearchByDevEUIAsync(devEUI))
                .ReturnsAsync(new SearchDevicesResult(
                    new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            var twin = simulatedDevice.CreateABPTwin(reportedProperties: new Dictionary<string, object>
            {
                { TwinProperty.Region, LoRaRegionType.EU868.ToString() }
            });
            this.deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;

            var c2dPayloadSize = this.loRaRegion.GetMaxPayloadSize(this.loRaRegion.GetDefaultRX2ReceiveWindow().DataRate)
                - c2dMessageMacCommandSize
                - Constants.LoraProtocolOverheadSize;

            var c2dMsgPayload = GeneratePayload("123457890", (int)c2dPayloadSize);
            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = devEUI,
                Payload = c2dMsgPayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2d.MacCommands.ResetTo(new[] { c2dMessageMacCommand });
            }

            using var cloudToDeviceMessage = c2d.CreateMessage();

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                PacketForwarder,
                this.frameCounterStrategyProvider);

            // Expectations
            // Verify that C2D message is sent
            Assert.True(await target.SendAsync(c2d));

            // Verify that exactly one C2D message was received
            Assert.Single(PacketForwarder.DownlinkMessages);

            // Verify donwlink message is correct
            EnsureDownlinkIsCorrect(
                PacketForwarder.DownlinkMessages.First(), simulatedDevice, c2d);

            // Get C2D message payload
            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(
                Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(simulatedDevice.AppSKey);

            // Verify that expected Mac commands are present
            var expectedMacCommandsCount = 0;

            if (hasMacInC2D)
                expectedMacCommandsCount++;

            if (expectedMacCommandsCount > 0)
            {
                var macCommands = MacCommand.CreateServerMacCommandFromBytes(
                    simulatedDevice.DevEUI, payloadDataDown.Fopts);
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
                    1, deviceClassType: 'c', gatewayID: this.serverConfiguration.GatewayID));

            var devEUI = simulatedDevice.DevEUI;

            this.deviceApi.Setup(x => x.SearchByDevEUIAsync(devEUI))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(string.Empty, devEUI, "123").AsList()));

            this.deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;

            var c2dPayloadSize = this.loRaRegion.GetMaxPayloadSize(this.loRaRegion.GetDefaultRX2ReceiveWindow().DataRate)
                - c2dMessageMacCommandSize
                + 1 // make message too long on purpose
                - Constants.LoraProtocolOverheadSize;

            var c2dMsgPayload = GeneratePayload("123457890", (int)c2dPayloadSize);
            var c2d = new ReceivedLoRaCloudToDeviceMessage()
            {
                DevEUI = devEUI,
                Payload = c2dMsgPayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2d.MacCommands.ResetTo(new[] { c2dMessageMacCommand });
            }

            using var cloudToDeviceMessage = c2d.CreateMessage();

            var target = new DefaultClassCDevicesMessageSender(
                this.serverConfiguration,
                this.loRaDeviceRegistry,
                PacketForwarder,
                this.frameCounterStrategyProvider);

            // Expectations
            // Verify that C2D message is sent
            Assert.False(await target.SendAsync(c2d));

            // Verify that exactly one C2D message was received
            Assert.Empty(PacketForwarder.DownlinkMessages);

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

        public void Dispose()
        {
            this.loRaDeviceRegistry.Dispose();
            this.cache.Dispose();
            this.loRaDeviceFactory.Dispose();
        }
    }
}
