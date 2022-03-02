// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System.Threading;
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

    public class LoRaDataRequestHandlerConnectionAffinityIntegrationTests : MessageProcessorTestBase
    {
        private readonly MemoryCache cache;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategy> frameCounterStrategyMock;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider> frameCounterProviderMock;
        private readonly Mock<TestDefaultLoRaRequestHandler> dataRequestHandlerMock;
        private readonly SimulatedDevice simulatedABPDevice;
        private readonly Mock<LoRaDevice> deviceMock;
        private readonly WaitableLoRaRequest loraRequest;

        public LoRaDataRequestHandlerConnectionAffinityIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);
            var concentratorDeduplication = new ConcentratorDeduplication(this.cache, this.testOutputLoggerFactory.CreateLogger<IConcentratorDeduplication>());

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

            this.simulatedABPDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            this.deviceMock = new Mock<LoRaDevice>(
                MockBehavior.Default,
                this.simulatedABPDevice.DevAddr,
                this.simulatedABPDevice.DevEUI,
                ConnectionManager);

            _ = this.frameCounterStrategyMock.Setup(x => x.NextFcntDown(this.deviceMock.Object, It.IsAny<uint>())).Returns(() => ValueTask.FromResult<uint>(1));
            _ = this.frameCounterProviderMock.Setup(x => x.GetStrategy(this.deviceMock.Object.GatewayID)).Returns(this.frameCounterStrategyMock.Object);

            var message = this.simulatedABPDevice.CreateUnconfirmedDataUpMessage("payload", fcnt: 1);
            this.loraRequest = CreateWaitableRequest(message);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(null, true)]
        public async Task When_Gateway_Does_Not_Own_Connection_It_Should_Delay_If_Delay_Enabled(bool? connectionOwner, bool processingDelayEnabled)
        {
            // arrange
            this.deviceMock.Object.IsConnectionOwner = connectionOwner;
            this.dataRequestHandlerMock.Setup(x => x.IsProcessingDelayEnabled()).Returns(processingDelayEnabled);
            
            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.deviceMock.Object);

            // assert
            var shouldDelay = connectionOwner is false && processingDelayEnabled;
            this.dataRequestHandlerMock.Verify(x => x.DelayProcessingAssert(), shouldDelay ? Times.Once : Times.Never);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public async Task Connection_Handling_Should_Depend_On_Deduplication_Result_And_Processing_Delay(bool canProcess, bool processingDelayEnabled)
        {
            // arrange
            this.deviceMock.Object.IsConnectionOwner = true;
            _ = this.dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                { CanProcess = canProcess },
                NextFCntDown = null
            });

            this.dataRequestHandlerMock.Setup(x => x.IsProcessingDelayEnabled()).Returns(processingDelayEnabled);

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.deviceMock.Object);

            // assert
            Assert.Equal(canProcess || !processingDelayEnabled, this.deviceMock.Object.IsConnectionOwner);

            this.deviceMock.Verify(x => x.BeginDeviceClientConnectionActivity(), canProcess ? Times.Once : Times.Never);

            if (canProcess || !processingDelayEnabled)
            {
                this.deviceMock.Verify(x => x.CloseConnectionAsync(CancellationToken.None, It.IsAny<bool>()), Times.Never);
                this.dataRequestHandlerMock.Verify(x => x.SaveChangesToDeviceAsyncAssert(), Times.Once);
            }
            else
            {
                this.deviceMock.Verify(x => x.CloseConnectionAsync(CancellationToken.None, false), Times.Once);
                this.dataRequestHandlerMock.Verify(x => x.SaveChangesToDeviceAsyncAssert(), Times.Never);
            }
        }

        protected override async ValueTask DisposeAsync(bool disposing)
        {
            if (disposing)
            {
                this.cache.Dispose();
                this.loraRequest.Dispose();
                this.testOutputLoggerFactory.Dispose();
            }
            await base.DisposeAsync(disposing);
        }
    }
}
