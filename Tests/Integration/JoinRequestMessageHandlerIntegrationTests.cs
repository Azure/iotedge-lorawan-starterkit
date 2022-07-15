// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using LoRaTools.CommonAPI;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public sealed class JoinRequestMessageHandlerIntegrationTests : MessageProcessorTestBase
    {
        private readonly MemoryCache cache;
        private readonly Mock<LoRaDeviceAPIServiceBase> apiServiceMock;
        private readonly JoinRequestMessageHandler joinRequestHandler;
        private readonly SimulatedDevice simulatedDevice;
        private readonly Mock<LoRaDevice> deviceMock;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;
        private readonly Mock<ILoRaDeviceRegistry> deviceRegistryMock;
        private readonly Mock<ILoRaDeviceClient> clientMock;
        private bool disposedValue;

        public JoinRequestMessageHandlerIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            this.deviceMock = new Mock<LoRaDevice>(MockBehavior.Default,
                this.simulatedDevice.DevAddr,
                this.simulatedDevice.DevEUI,
                ConnectionManager)
            {
                CallBase = true
            };
            this.deviceMock.Object.AppKey = this.simulatedDevice.AppKey;
            this.deviceMock.Object.AppEui = this.simulatedDevice.AppEui;
            this.deviceMock.Object.IsOurDevice = true;
            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);

            this.cache = new MemoryCache(new MemoryCacheOptions());
            var concentratorDeduplication = new ConcentratorDeduplication(this.cache, this.testOutputLoggerFactory.CreateLogger<IConcentratorDeduplication>());
            this.deviceRegistryMock = new Mock<ILoRaDeviceRegistry>();
            _ = this.deviceRegistryMock.Setup(x => x.GetDeviceForJoinRequestAsync(It.IsAny<DevEui>(), It.IsAny<DevNonce>()))
                .ReturnsAsync(this.deviceMock.Object);

            this.clientMock = new Mock<ILoRaDeviceClient>();
            _ = this.clientMock.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ConnectionManager.Register(this.deviceMock.Object, this.clientMock.Object);

            this.apiServiceMock = new Mock<LoRaDeviceAPIServiceBase>();

            this.joinRequestHandler = new JoinRequestMessageHandler(
                ServerConfiguration,
                concentratorDeduplication,
                this.deviceRegistryMock.Object,
                this.testOutputLoggerFactory.CreateLogger<JoinRequestMessageHandler>(),
                this.apiServiceMock.Object,
                null);
        }

        [Fact]
        public async Task When_Same_Join_Request_Received_Multiple_Times_Succeeds_Only_Once()
        {
            var joinRequest = this.simulatedDevice.CreateJoinRequest();
            var loraRequest = CreateWaitableRequest(joinRequest);
            loraRequest.SetPayload(joinRequest);
            loraRequest.SetRegion(new RegionEU868());

            // first request
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);

            // repeat
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);

            // assert
            this.deviceMock.Verify(x => x.UpdateAfterJoinAsync(It.IsAny<LoRaDeviceJoinUpdateProperties>(), It.IsAny<CancellationToken>()), Times.Once());
            this.deviceRegistryMock.Verify(x => x.UpdateDeviceAfterJoin(It.IsAny<LoRaDevice>(), null), Times.Once());
            this.apiServiceMock.Verify(x => x.SendJoinNotificationAsync(It.IsAny<DeviceJoinNotification>(), It.IsAny<CancellationToken>()), Times.Once());

            // do another request
            joinRequest = this.simulatedDevice.CreateJoinRequest();
            loraRequest.SetPayload(joinRequest);
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);
            this.deviceMock.Verify(x => x.UpdateAfterJoinAsync(It.IsAny<LoRaDeviceJoinUpdateProperties>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            this.deviceRegistryMock.Verify(x => x.UpdateDeviceAfterJoin(It.IsAny<LoRaDevice>(), It.IsNotNull<DevAddr>()), Times.Once());
            this.apiServiceMock.Verify(x => x.SendJoinNotificationAsync(It.IsAny<DeviceJoinNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Ensures_Connection_Closed_After_Every_Join(bool joinHandledByAnotherGateway)
        {
            // arrange
            var joinRequest = this.simulatedDevice.CreateJoinRequest();
            var loraRequest = CreateWaitableRequest(joinRequest);
            loraRequest.SetPayload(joinRequest);
            loraRequest.SetRegion(new RegionEU868());

            if (joinHandledByAnotherGateway)
                this.deviceMock.Object.DevNonce = joinRequest.DevNonce;

            // act
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);

            // assert
            this.deviceMock.Verify(x => x.CloseConnectionAsync(CancellationToken.None, true), Times.Once);
            if (!joinHandledByAnotherGateway)
            {
                this.deviceMock.Verify(x => x.UpdateAfterJoinAsync(It.IsAny<LoRaDeviceJoinUpdateProperties>(), It.IsAny<CancellationToken>()), Times.Once());
                this.deviceRegistryMock.Verify(x => x.UpdateDeviceAfterJoin(It.IsAny<LoRaDevice>(), null), Times.Once());
            }
            this.apiServiceMock.Verify(x => x.SendJoinNotificationAsync(It.IsAny<DeviceJoinNotification>(), It.IsAny<CancellationToken>()), joinHandledByAnotherGateway ? Times.Never() : Times.Once());

            // act and assert again
            joinRequest = this.simulatedDevice.CreateJoinRequest();
            loraRequest.SetPayload(joinRequest);
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);
            this.deviceMock.Verify(x => x.CloseConnectionAsync(CancellationToken.None, true), Times.Exactly(2));
            this.apiServiceMock.Verify(x => x.SendJoinNotificationAsync(It.IsAny<DeviceJoinNotification>(), It.IsAny<CancellationToken>()), joinHandledByAnotherGateway ? Times.Once() : Times.Exactly(2));
        }

        [Fact]
        public async Task Ensures_Registry_Update_Only_Invoked_When_Twin_Updated()
        {
            var joinRequest = this.simulatedDevice.CreateJoinRequest();
            var loraRequest = CreateWaitableRequest(joinRequest);
            loraRequest.SetPayload(joinRequest);
            loraRequest.SetRegion(new RegionEU868());

            _ = this.clientMock.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            // first request will not be able to update twin
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);

            // assert
            this.deviceMock.Verify(x => x.UpdateAfterJoinAsync(It.IsAny<LoRaDeviceJoinUpdateProperties>(), It.IsAny<CancellationToken>()), Times.Once());
            this.deviceRegistryMock.Verify(x => x.UpdateDeviceAfterJoin(It.IsAny<LoRaDevice>(), It.IsAny<DevAddr>()), Times.Never());
            this.apiServiceMock.Verify(x => x.SendJoinNotificationAsync(It.IsAny<DeviceJoinNotification>(), It.IsAny<CancellationToken>()), Times.Never());

            // do another request, which will succeed and therefore deviceRegistry should be updated
            _ = this.clientMock.Setup(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            joinRequest = this.simulatedDevice.CreateJoinRequest();
            loraRequest.SetPayload(joinRequest);
            await this.joinRequestHandler.ProcessJoinRequestAsync(loraRequest);
            this.deviceMock.Verify(x => x.UpdateAfterJoinAsync(It.IsAny<LoRaDeviceJoinUpdateProperties>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            // asserting that a UpdateDeviceAfterJoin with a null "oldDevAddr" is received meaning that device was not in dev Addr cache
            this.deviceRegistryMock.Verify(x => x.UpdateDeviceAfterJoin(It.IsAny<LoRaDevice>(), null), Times.Once());
            this.apiServiceMock.Verify(x => x.SendJoinNotificationAsync(It.IsAny<DeviceJoinNotification>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        protected override async ValueTask DisposeAsync(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.cache.Dispose();
                    this.testOutputLoggerFactory.Dispose();
                }

                this.disposedValue = true;
            }

            // Call base class implementation.
            await base.DisposeAsync(disposing);
        }
    }
}
