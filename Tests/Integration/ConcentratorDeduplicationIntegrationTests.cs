// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using Common;
    using LoRaTools;
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
                return Task.FromResult(PerformADRAssert());
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

            public virtual LoRaADRResult PerformADRAssert() => null;

            public virtual FunctionBundlerResult TryUseBundlerAssert() => null;

            public virtual bool SendDeviceAsyncAssert() => true;

            public virtual Task SendMessageDownstreamAsyncAssert() => null;

            public virtual bool SaveChangesToDeviceAssert() => true;
        }

        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategy> frameCounterStrategyMock;
        private readonly Mock<TestDefaultLoRaRequestHandler> dataRequestHandlerMock;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider> frameCounterProviderMock;
        private readonly MemoryCache cache;

        public ConcentratorDeduplicationIntegrationTests()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            var concentratorDeduplication = new ConcentratorDeduplication(this.cache, NullLogger<IConcentratorDeduplication>.Instance);

            this.frameCounterStrategyMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategy>();
            this.frameCounterProviderMock = new Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider>();

            this.dataRequestHandlerMock = new Mock<TestDefaultLoRaRequestHandler>(MockBehavior.Default,
                ServerConfiguration,
                this.frameCounterProviderMock.Object,
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

            _ = this.dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                { IsDuplicate = false, CanProcess = true },
                NextFCntDown = null
            });
        }


        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 2, 1, 2, 2, 2, 2, 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 1, 1, 1, 1, 2, 1, 2)]
        public async Task When_ADR_Enabled_PerformADR_Should_Be_Disabled_For_SoftDuplicate(
            string station1,
            string station2,
            int expectedNumberOfADRCalls,
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
            dataPayload.Fctrl.Span[0] = 250; // adr enabled

            var request1 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            request1.SetStationEui(StationEui.Parse(station1));
            request1.SetPayload(dataPayload);

            using var request2 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            request2.SetStationEui(StationEui.Parse(station2));
            request2.SetPayload(dataPayload);

            using var device = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = DeduplicationMode.Mark, // or None
                NwkSKey = station1
            };

            // act/assert
            await TestAssertions(request1, request2, device, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);

            this.dataRequestHandlerMock.Verify(x => x.PerformADRAssert(), Times.Exactly(expectedNumberOfADRCalls));
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 1, 2, 2, 0, 2, 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 1, 1, 1, 0, 1, 2)]
        public async Task When_Mac_Command_Should_Not_Send_Upstream_Messages_And_Should_Skip_Calls_To_FrameCounterDown_When_SoftDuplicate(
            string station1,
            string station2,
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
            // MAC command
            dataPayload.Fport = new byte[1] { 0 };
            dataPayload.MacCommands = new List<MacCommand> { new LinkCheckAnswer(1, 1) };

            var request1 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            request1.SetStationEui(StationEui.Parse(station1));
            request1.SetPayload(dataPayload);

            using var request2 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            request2.SetStationEui(StationEui.Parse(station2));
            request2.SetPayload(dataPayload);

            using var device = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = DeduplicationMode.None, // or Mark
                NwkSKey = station1
            };

            // act/assert
            await TestAssertions(request1, request2, device, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 1, 2, 0, 2, 2, 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 1, 1, 0, 2, 1, 2)]
        public async Task When_Bundler_Returns_FrameCounterDown_Should_Skip_Call_To_FrameCounterDown_Independently_Of_Deduplication_Result(
            string station1,
            string station2,
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

            var request1 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            request1.SetStationEui(StationEui.Parse(station1));
            request1.SetPayload(dataPayload);

            using var request2 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            request2.SetStationEui(StationEui.Parse(station2));
            request2.SetPayload(dataPayload);

            using var device = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = DeduplicationMode.Mark, // or Drop, None
                NwkSKey = station1
            };

            _ = this.dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                { IsDuplicate = false, CanProcess = true },
                NextFCntDown = 1
            });

            // act/assert
            await TestAssertions(request1, request2, device, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", null, 1, 2, 2, 2, 2, 2)] // resubmission 
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, 1, 1, 1, 1, 1, 1)] // duplicate
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, 1, 1, 1, 2, 1, 2)] // soft duplicate, due to DeduplicationMode.Mark
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, 1, 1, 1, 2, 1, 2)] // soft duplicate but due to DeduplicationMode.None
        public async Task When_Default_Data_Message_Test_All_Different_DeduplicationModes(
            string station1,
            string station2,
            DeduplicationMode? deduplicationMode,
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

            using var loraRequest1 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            loraRequest1.SetStationEui(StationEui.Parse(station1));
            loraRequest1.SetPayload(dataPayload);

            using var loraRequest2 = CreateWaitableRequest(dataPayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0]);
            loraRequest2.SetStationEui(StationEui.Parse(station2));
            loraRequest2.SetPayload(dataPayload);

            using var loRaDevice = new LoRaDevice(simulatedDevice.DevAddr, simulatedDevice.DevEUI, ConnectionManager)
            {
                Deduplication = deduplicationMode ?? DeduplicationMode.Drop, // fallback to default
                NwkSKey = station1
            };

            // act/assert
            await TestAssertions(loraRequest1, loraRequest2, loRaDevice, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        private async Task TestAssertions(
            LoRaRequest request1,
            LoRaRequest request2,
            LoRaDevice device,
            int expectedNumberOfFrameCounterResets,
            int expectedNumberOfBundlerCalls,
            int expectedNumberOfFrameCounterDownCalls,
            int expectedMessagesUp,
            int expectedMessagesDown,
            int expectedTwinSaves)
        {
            _ = this.frameCounterStrategyMock.Setup(x => x.NextFcntDown(device, It.IsAny<uint>())).Returns(() => ValueTask.FromResult<uint>(1));
            _ = this.frameCounterProviderMock.Setup(x => x.GetStrategy(device.GatewayID)).Returns(this.frameCounterStrategyMock.Object);

            // first request
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request1, device);

            // act
            var actual = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request2, device);

            // assert
            this.frameCounterStrategyMock.Verify(x => x.ResetAsync(device, It.IsAny<uint>(), ServerGatewayID), Times.Exactly(expectedNumberOfFrameCounterResets));
            this.frameCounterStrategyMock.Verify(x => x.NextFcntDown(device, It.IsAny<uint>()), Times.Exactly(expectedNumberOfFrameCounterDownCalls));
            this.dataRequestHandlerMock.Verify(x => x.TryUseBundlerAssert(), Times.Exactly(expectedNumberOfBundlerCalls));
            this.dataRequestHandlerMock.Verify(x => x.SendDeviceAsyncAssert(), Times.Exactly(expectedMessagesUp));
            this.dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(), Times.Exactly(expectedMessagesDown));
            this.dataRequestHandlerMock.Verify(x => x.SaveChangesToDeviceAssert(), Times.Exactly(expectedTwinSaves));
        }

        // Protected implementation of Dispose pattern.
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.cache.Dispose();
            }
        }
    }
}
