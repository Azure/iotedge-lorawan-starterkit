// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System.Threading.Tasks;
    using Common;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationIntegrationTests : MessageProcessorTestBase
    {
        private WaitableLoRaRequest loraRequest;
        private LoRaDevice loRaDevice;

        /// <summary>
        /// This test integrates <code>DefaultLoRaDataRequestHandler</code> with <code>ConcentratorDeduplication</code>
        /// with a partial mock on <code>DefaultLoRaDataRequestHandler</code> that stops execution after the deduplication takes place.
        /// </summary>
        /// <param name="station1"></param>
        /// <param name="station2"></param>
        /// <param name="expectedFailure"></param>
        /// <returns></returns>
        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, LoRaDeviceRequestFailedReason.UnknownDevice)]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, LoRaDeviceRequestFailedReason.UnknownDevice)]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, LoRaDeviceRequestFailedReason.UnknownDevice)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, LoRaDeviceRequestFailedReason.DeduplicationDrop)]
        public async Task When_Same_Data_Message_Comes_Multiple_Times_Result_Depends_On_Which_Concentrator_It_Was_Sent_From(string station1, string station2, DeduplicationMode deduplicationMode, LoRaDeviceRequestFailedReason expectedFailure)
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateConfirmedDataUpMessage("payload");
            this.loraRequest = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            this.loraRequest.SetStationEui(StationEui.Parse(station1));
            this.loraRequest.SetPayload(dataPayload);

            this.loRaDevice = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = deduplicationMode
            };

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var concentratorDeduplication = new ConcentratorDeduplication(cache, NullLogger<IConcentratorDeduplication>.Instance);

            var dataRequestHandlerMock = new Mock<DefaultLoRaDataRequestHandler>(MockBehavior.Default,
                ServerConfiguration,
                FrameCounterUpdateStrategyProvider,
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
            _ = dataRequestHandlerMock.Setup(x => x.DetermineIfFramecounterIsFromNewlyStartedDeviceAsync(It.IsAny<LoRaDevice>(), 1, It.IsAny<ILoRaDeviceFrameCounterUpdateStrategy>(), It.IsAny<ConcentratorDeduplication.Result>()))
                .ReturnsAsync(true);
            var result1 = true;
            var result2 = new LoRaDeviceRequestProcessResult(this.loRaDevice, this.loraRequest, LoRaDeviceRequestFailedReason.UnknownDevice);
            _ = dataRequestHandlerMock.Setup(x => x.ValidateRequest(It.IsAny<LoRaRequest>(), It.IsAny<bool>(), It.IsAny<uint>(), It.IsAny<LoRaDevice>(), out result1, out result2))
                .Returns(false);

            // first request
            _ = await dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);

            // act
            this.loraRequest.SetStationEui(StationEui.Parse(station2));
            var actual = await dataRequestHandlerMock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);

            // assert
            Assert.Equal(expectedFailure, actual.FailedReason);
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
