// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class ConnectionAffinityIntegrationTests : MessageProcessorTestBase
    {
        private readonly MemoryCache cache;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategy> frameCounterStrategyMock;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider> frameCounterProviderMock;
        private readonly Mock<TestDefaultLoRaRequestHandler> dataRequestHandlerMock;
        private readonly SimulatedDevice simulatedABPDevice;
        private readonly Mock<LoRaDevice> deviceMock;

        public ConnectionAffinityIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);
            var concentratorDeduplication = new ConcentratorDeduplication(cache, this.testOutputLoggerFactory.CreateLogger<IConcentratorDeduplication>());

            this.frameCounterStrategyMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategy>();
            this.frameCounterProviderMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider>();

            this.dataRequestHandlerMock = new Mock<TestDefaultLoRaRequestHandler>(MockBehavior.Default,
                ServerConfiguration,
                this.frameCounterProviderMock.Object,
                concentratorDeduplication,
                PayloadDecoder,
                new DeduplicationStrategyFactory(this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<DeduplicationStrategyFactory>()),
                new LoRaADRStrategyProvider(this.testOutputLoggerFactory),
                new LoRAADRManagerFactory(LoRaDeviceApi.Object, this.testOutputLoggerFactory),
                new FunctionBundlerProvider(LoRaDeviceApi.Object, this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<FunctionBundlerProvider>()),
                testOutputHelper)
            {
                CallBase = true
            };

            _ = this.dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                { IsDuplicate = true, CanProcess = false },
                NextFCntDown = null
            });

            this.simulatedABPDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            this.deviceMock = new Mock<LoRaDevice>(
                MockBehavior.Default,
                this.simulatedABPDevice.DevAddr,
                this.simulatedABPDevice.DevEUI,
                ConnectionManager);
            this.deviceMock.Object.AppSKey = this.simulatedABPDevice.AppSKey;
            this.deviceMock.Object.NwkSKey = this.simulatedABPDevice.NwkSKey;

            _ = this.frameCounterStrategyMock.Setup(x => x.NextFcntDown(this.deviceMock.Object, It.IsAny<uint>())).Returns(() => ValueTask.FromResult<uint>(1));
            _ = this.frameCounterProviderMock.Setup(x => x.GetStrategy(this.deviceMock.Object.GatewayID)).Returns(this.frameCounterStrategyMock.Object);
        }

        [Fact]
        public async Task Test()
        {
            // arrange
            var message = this.simulatedABPDevice.CreateUnconfirmedDataUpMessage("payload", fcnt: 1);
            var loraRequest = CreateWaitableRequest(message);
            this.deviceMock.Object.IsConnectionOwner = true;

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(loraRequest, this.deviceMock.Object);

            // assert
            Assert.False(this.deviceMock.Object.IsConnectionOwner);
            this.deviceMock.Verify(x => x.CloseConnection(It.IsAny<bool>()), Times.Once);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.cache.Dispose();
                this.testOutputLoggerFactory.Dispose();
            }
        }
    }
}
