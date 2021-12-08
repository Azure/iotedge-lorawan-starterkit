// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using Common;
    using LoRaTools.ADR;
    using LoRaTools.Regions;
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

            protected override Task<bool> SendDeviceEventAsync(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, object decodedValue, bool isDuplicate, byte[] decryptedPayloadData)
            {
                SendDeviceAsyncAssert();
                return Task.FromResult(true);
            }

            protected override Task<FunctionBundlerResult> TryUseBundler(LoRaRequest request, LoRaDevice loRaDevice, LoRaTools.LoRaMessage.LoRaPayloadData loraPayload, bool useMultipleGateways)
            {
                return Task.FromResult(TryUseBundlerAssert());
            }

            protected override Task SaveChangesToDevice(LoRaDevice loRaDevice, bool stationEuiChanged)
            {
                SaveChangesToDeviceAssert();
                return Task.FromResult(true);
            }

            protected override Task SendMessageDownstreamAsync(LoRaRequest request, DownlinkMessageBuilderResponse confirmDownlinkMessageBuilderResp)
            {
                return Task.FromResult(SendMessageDownstreamAsyncAssert());
            }

            protected override Task<IReceivedLoRaCloudToDeviceMessage> ReceiveCloudToDeviceAsync(LoRaDevice loRaDevice, TimeSpan timeAvailableToCheckCloudToDeviceMessages)
            {
                return Task.FromResult<IReceivedLoRaCloudToDeviceMessage>(null);
            }

            protected override DownlinkMessageBuilderResponse DownlinkMessageBuilderResponse(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, LoRaADRResult loRaADRResult, IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage, uint? fcntDown, bool fpending)
            {
                return new DownlinkMessageBuilderResponse(new LoRaTools.LoRaPhysical.DownlinkPktFwdMessage(), false, 1);
            }

            public virtual void SendDeviceAsyncAssert() { }

            public virtual FunctionBundlerResult TryUseBundlerAssert() => null;

            public virtual Task SendMessageDownstreamAsyncAssert() => null;

            public virtual void SaveChangesToDeviceAssert() { }
        }

        private WaitableLoRaRequest loraRequest;
        private LoRaDevice loRaDevice;

        /// <summary>
        /// This test integrates <code>DefaultLoRaDataRequestHandler</code> with <code>ConcentratorDeduplication</code>.
        /// </summary>
        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, 1, 1, 1, 1, 1)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, 1, 1, 2, 1, 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, 1, 1, 2, 1, 2)]
        public async Task When_Same_Data_Message_Comes_Multiple_Times_Result_Depends_On_Which_Concentrator_It_Was_Sent_From(
            string station1, string station2, DeduplicationMode deduplicationMode,  int expectedNumberOfFrameCounterResets, int expectedNumberOfFunctionCalls, int expectedMessagesUp, int expectedMessagesDown, int expectedTwinSaves)
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateConfirmedDataUpMessage("payload");
            this.loraRequest = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            this.loraRequest.SetStationEui(StationEui.Parse(station1));
            this.loraRequest.SetPayload(dataPayload);
            this.loraRequest.SetRegion(new RegionEU868());

            this.loRaDevice = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = deduplicationMode,
                NwkSKey = station1
            };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var concentratorDeduplication = new ConcentratorDeduplication(cache, NullLogger<IConcentratorDeduplication>.Instance);

            var frameCounterStrategyMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategy>();
            var frameCounterProviderMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider>();
            frameCounterProviderMock.Setup(x => x.GetStrategy(this.loRaDevice.GatewayID)).Returns(frameCounterStrategyMock.Object);

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
            dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult { DeduplicationResult = new DeduplicationResult { IsDuplicate = false, CanProcess = true }, NextFCntDown = 1 });

            // first request
            _ = await dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);

            // act
            this.loraRequest.SetStationEui(StationEui.Parse(station2));
            var actual = await dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);

            // assert
            frameCounterStrategyMock.Verify(x => x.ResetAsync(this.loRaDevice, It.IsAny<uint>(), ServerGatewayID), Times.Exactly(expectedNumberOfFrameCounterResets));
            dataRequestHandlerMock.Verify(x => x.TryUseBundlerAssert(), Times.Exactly(expectedNumberOfFunctionCalls));
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
