// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using Common;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationIntegrationTests : MessageProcessorTestBase
    {
        internal class TestDefaultLoRaRequestHandler : DefaultLoRaDataRequestHandler
        {
            public TestDefaultLoRaRequestHandler(
                NetworkServerConfiguration configuration,
                ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
                IConcentratorDeduplication concentratorDeduplication,
                ILoRaPayloadDecoder payloadDecoder,
                IDeduplicationStrategyFactory deduplicationFactory,
                ILoRaADRStrategyProvider loRaADRStrategyProvider,
                ILoRAADRManagerFactory loRaADRManagerFactory,
                IFunctionBundlerProvider functionBundlerProvider,
                ILogger<DefaultLoRaDataRequestHandler> logger,
                Meter meter) : base(
                    configuration,
                    frameCounterUpdateStrategyProvider,
                    concentratorDeduplication,
                    payloadDecoder,
                    deduplicationFactory,
                    loRaADRStrategyProvider,
                    loRaADRManagerFactory,
                    functionBundlerProvider,
                    logger,
                    meter)
            { }

            protected override Task<FunctionBundlerResult> TryUseBundler(LoRaRequest request, LoRaDevice loRaDevice, LoRaTools.LoRaMessage.LoRaPayloadData loraPayload, bool useMultipleGateways)
                => Task.FromResult(TryUseBundlerAssert());

            protected override Task<LoRaADRResult> PerformADR(LoRaRequest request, LoRaDevice loRaDevice, LoRaPayloadData loraPayload, uint payloadFcnt, LoRaADRResult loRaADRResult, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
            {
                return Task.FromResult<LoRaADRResult>(null);
            }

            protected override Task<IReceivedLoRaCloudToDeviceMessage> ReceiveCloudToDeviceAsync(LoRaDevice loRaDevice, TimeSpan timeAvailableToCheckCloudToDeviceMessages)
                => Task.FromResult<IReceivedLoRaCloudToDeviceMessage>(null);

            protected override Task<bool> SendDeviceEventAsync(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, object decodedValue, bool isDuplicate, byte[] decryptedPayloadData)
                => Task.FromResult(SendDeviceAsyncAssert());

            protected override DownlinkMessageBuilderResponse DownlinkMessageBuilderResponse(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, LoRaADRResult loRaADRResult, IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage, uint? fcntDown, bool fpending)
                => new DownlinkMessageBuilderResponse(new LoRaTools.LoRaPhysical.DownlinkPktFwdMessage(), false, 1);

            protected override Task SendMessageDownstreamAsync(LoRaRequest request, DownlinkMessageBuilderResponse confirmDownlinkMessageBuilderResp)
                => Task.FromResult(SendMessageDownstreamAsyncAssert());

            protected override Task SaveChangesToDevice(LoRaDevice loRaDevice, bool stationEuiChanged)
                => Task.FromResult(SaveChangesToDeviceAssert());

            public virtual FunctionBundlerResult TryUseBundlerAssert() => null;

            public virtual bool SendDeviceAsyncAssert() => true;

            public virtual Task SendMessageDownstreamAsyncAssert() => null;

            public virtual bool SaveChangesToDeviceAssert() => true;
        }

        private WaitableLoRaRequest loraRequest;
        private LoRaDevice loRaDevice;

        /// <summary>
        /// This test integrates <code>DefaultLoRaDataRequestHandler</code> with <code>ConcentratorDeduplication</code>.
        /// </summary>
        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", null, true, true, 1, 2, 0, 2, 2, 2)] // resubmission 
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", null, true, null, 1, 2, 2, 2, 2, 2)] // resubmission, with extra call to get framecounter down
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, false, null, 1, 1, 1, 1, 1, 1)] // duplicate
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, false, true, 1, 1, 0, 2, 1, 2)] // soft duplicate
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, false, null, 1, 1, 1, 2, 1, 2)] // soft duplicate, with extra call to get framecounter down
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, true, true, 1, 1, 0, 2, 1, 2)] // soft duplicate, adr request
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, false, true, 1, 1, 0, 2, 1, 2)] // soft duplicate but due to DeduplicationMode.None
        public async Task When_Same_Data_Message_Comes_Multiple_Times_Result_Depends_On_Which_Concentrator_It_Was_Sent_From(
            string station1,
            string station2,
            DeduplicationMode? deduplicationMode,
            bool isAdrRequest,
            bool? frameCounterResultFromBundler,
            int expectedNumberOfFrameCounterResets,
            int expectedNumberOfBundlerCalls,
            int expectedNumberOfFrameCounterDownCalls,
            int expectedMessagesUp,
            int expectedMessagesDown,
            int expectedTwinSaves)
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateConfirmedDataUpMessage("payload");
            if (isAdrRequest)
                dataPayload.Fctrl.Span[0] = 250;
            this.loraRequest = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            this.loraRequest.SetStationEui(StationEui.Parse(station1));
            this.loraRequest.SetPayload(dataPayload);

            this.loRaDevice = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = deduplicationMode ?? DeduplicationMode.Drop,
                NwkSKey = station1
            };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var concentratorDeduplication = new ConcentratorDeduplication(cache, NullLogger<IConcentratorDeduplication>.Instance);

            var frameCounterStrategyMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategy>();
            _ = frameCounterStrategyMock.Setup(x => x.NextFcntDown(this.loRaDevice, It.IsAny<uint>())).Returns(() => ValueTask.FromResult<uint>(1));
            var frameCounterProviderMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider>();
            _ = frameCounterProviderMock.Setup(x => x.GetStrategy(this.loRaDevice.GatewayID)).Returns(frameCounterStrategyMock.Object);

            var dataRequestHandlerMock = new Mock<TestDefaultLoRaRequestHandler>(MockBehavior.Default,
                ServerConfiguration,
                frameCounterProviderMock.Object,
                concentratorDeduplication,
                PayloadDecoder,
                new DeduplicationStrategyFactory(NullLoggerFactory.Instance, NullLogger<DeduplicationStrategyFactory>.Instance),
                new LoRaADRStrategyProvider(NullLoggerFactory.Instance),
                new LoRAADRManagerFactory(LoRaDeviceApi.Object, NullLoggerFactory.Instance),
                new FunctionBundlerProvider(LoRaDeviceApi.Object, NullLoggerFactory.Instance, NullLogger<FunctionBundlerProvider>.Instance),
                NullLogger<DefaultLoRaDataRequestHandler>.Instance,
                null)
            {
                CallBase = true
            };
            _ = dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                    { IsDuplicate = false, CanProcess = true },
                NextFCntDown = (frameCounterResultFromBundler is true) ? 1 : null
            });

            // first request
            _ = await dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);
            // assert methods are called once
            frameCounterStrategyMock.Verify(x => x.ResetAsync(this.loRaDevice, It.IsAny<uint>(), ServerGatewayID), Times.Once);
            dataRequestHandlerMock.Verify(x => x.TryUseBundlerAssert(), Times.Once);
            dataRequestHandlerMock.Verify(x => x.SendDeviceAsyncAssert(), Times.Once);
            dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(), Times.Once);
            dataRequestHandlerMock.Verify(x => x.SaveChangesToDeviceAssert(), Times.Once);

            // act
            this.loraRequest.SetStationEui(StationEui.Parse(station2));
            var actual = await dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);

            // assert
            frameCounterStrategyMock.Verify(x => x.ResetAsync(this.loRaDevice, It.IsAny<uint>(), ServerGatewayID), Times.Exactly(expectedNumberOfFrameCounterResets));
            frameCounterStrategyMock.Verify(x => x.NextFcntDown(this.loRaDevice, It.IsAny<uint>()), Times.Exactly(expectedNumberOfFrameCounterDownCalls));
            dataRequestHandlerMock.Verify(x => x.TryUseBundlerAssert(), Times.Exactly(expectedNumberOfBundlerCalls));
            dataRequestHandlerMock.Verify(x => x.SendDeviceAsyncAssert(), Times.Exactly(expectedMessagesUp));
            dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(), Times.Exactly(expectedMessagesDown));
            dataRequestHandlerMock.Verify(x => x.SaveChangesToDeviceAssert(), Times.Exactly(expectedTwinSaves));
        }

        // Protected implementation of Dispose pattern.
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.loRaDevice.Dispose();
                this.loraRequest.Dispose();
            }
        }
    }
}
