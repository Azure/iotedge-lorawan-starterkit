// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System.Threading.Tasks;
    using Common;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationJoinRequestsIntegrationTests : MessageProcessorTestBase
    {
        private readonly MemoryCache cache;
        private readonly JoinRequestMessageHandler joinRequestHandler;
        private readonly SimulatedDevice simulatedDevice;
        private readonly Mock<LoRaDevice> device;

        public ConcentratorDeduplicationJoinRequestsIntegrationTests()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            var concentratorDeduplication = new ConcentratorDeduplication(this.cache, NullLogger<IConcentratorDeduplication>.Instance);
            var deviceRegistryMock = new Mock<ILoRaDeviceRegistry>();

            this.joinRequestHandler = new JoinRequestMessageHandler(
                ServerConfiguration,
                concentratorDeduplication,
                deviceRegistryMock.Object,
                NullLogger<JoinRequestMessageHandler>.Instance,
                null);

            this.simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            this.device = new Mock<LoRaDevice>(MockBehavior.Default,
                this.simulatedDevice.DevAddr,
                this.simulatedDevice.DevEUI,
                ConnectionManager)
            {
                CallBase = true
            };
            this.device.Object.AppKey = this.simulatedDevice.AppKey;
            this.device.Object.AppEUI = this.simulatedDevice.AppEUI;
            this.device.Object.IsOurDevice = true;

            var clientMock = new Mock<ILoRaDeviceClient>();
            _ = clientMock.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>())).ReturnsAsync(true);
            ConnectionManager.Register(this.device.Object, clientMock.Object);

            _ = deviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(this.device.Object);
        }

        [Fact]
        public async Task When_Same_Join_Request_Received_Multiple_Times_Succeeds_Only_Once()
        {
            // arrange
            var joinRequest = this.simulatedDevice.CreateJoinRequest();
            var loraRequest = CreateWaitableRequest(joinRequest.SerializeUplink(this.simulatedDevice.AppKey).Rxpk[0]);
            loraRequest.SetPayload(joinRequest);
            loraRequest.SetRegion(new RegionEU868());

            // first request
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);

            // act
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);

            // assert
            this.device.Verify(x => x.UpdateAfterJoinAsync(It.IsAny<LoRaDeviceJoinUpdateProperties>()), Times.Once());
        }
    }
}
