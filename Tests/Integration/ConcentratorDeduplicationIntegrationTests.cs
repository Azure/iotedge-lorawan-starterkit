// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using Common;
    using LoRaTools.ADR;
    using LoRaTools.Regions;
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

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", LoRaDeviceRequestFailedReason.UnknownDevice)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", LoRaDeviceRequestFailedReason.DeduplicationDrop)]
        public async Task When_Same_Data_Message_Comes_Multiple_Times_Result_Depends_On_Which_Concentrator_It_Was_Sent_From(string station1, string station2, LoRaDeviceRequestFailedReason expectedFailure)
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = simulatedDevice.CreateConfirmedDataUpMessage("payload");
            this.loraRequest = WaitableLoRaRequest.Create(dataPayload);

            loraRequest.UseTimeWatcher(new TestLoRaOperationTimeWatcher(new RegionEU868(), new[] { TimeSpan.FromMilliseconds(10) }));
            loraRequest.SetStationEui(StationEui.Parse(station1));

            this.loRaDevice = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = DeduplicationMode.Drop
            };
            
            var deduplicationFactory = new DeduplicationStrategyFactory(NullLoggerFactory.Instance, NullLogger<DeduplicationStrategyFactory>.Instance);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var registryMock = new Mock<WebSocketWriterRegistry<StationEui, string>>(MockBehavior.Default, NullLogger<WebSocketWriterRegistry<StationEui, string>>.Instance);
            _ = registryMock.Setup(r => r.IsSocketWriterOpen(It.IsAny<StationEui>())).Returns(true);

            using var concentratorDeduplication = new ConcentratorDeduplication(cache, deduplicationFactory, registryMock.Object, NullLogger<IConcentratorDeduplication>.Instance);
            var mock = new Mock<DefaultLoRaDataRequestHandler>(MockBehavior.Default,
                ServerConfiguration,
                FrameCounterUpdateStrategyProvider,
                concentratorDeduplication,
                PayloadDecoder,
                new DeduplicationStrategyFactory(NullLoggerFactory.Instance, NullLogger<DeduplicationStrategyFactory>.Instance),
                new LoRaADRStrategyProvider(NullLoggerFactory.Instance),
                new LoRAADRManagerFactory(LoRaDeviceApi.Object, NullLoggerFactory.Instance),
                new FunctionBundlerProvider(LoRaDeviceApi.Object, NullLoggerFactory.Instance, NullLogger<FunctionBundlerProvider>.Instance),
                 NullLogger<DefaultLoRaDataRequestHandler>.Instance)
            {
                CallBase = true
            };
            _ = mock.Setup(x => x.DetermineIfFramecounterIsFromNewlyStartedDeviceAsync(It.IsAny<LoRaDevice>(), 1, It.IsAny<ILoRaDeviceFrameCounterUpdateStrategy>()))
                .ReturnsAsync(true);
            var result1 = true;
            var result2 = new LoRaDeviceRequestProcessResult(loRaDevice, loraRequest, LoRaDeviceRequestFailedReason.UnknownDevice);
            _ = mock.Setup(x => x.ValidateRequest(It.IsAny<LoRaRequest>(), It.IsAny<bool>(), It.IsAny<uint>(), It.IsAny<LoRaDevice>(), It.IsAny<bool>(), out result1, out result2))
                .Returns(false);

            _ = await mock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);

            // act
            this.loraRequest.SetStationEui(StationEui.Parse(station2));
            var actual = await mock.Object.ProcessRequestAsync(this.loraRequest, this.loRaDevice);

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
