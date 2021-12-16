// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Reflection;
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

    public sealed class ConcentratorDeduplicationDataMessagesIntegrationTests : MessageProcessorTestBase
    {
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategy> frameCounterStrategyMock;
        private readonly Mock<TestDefaultLoRaRequestHandler> dataRequestHandlerMock;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider> frameCounterProviderMock;
        private readonly MemoryCache cache;
        private readonly SimulatedDevice simulatedDevice;
        private readonly LoRaDevice loraDevice;

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

            protected override Task SaveChangesToDeviceAsync(LoRaDevice loRaDevice, bool stationEuiChanged)
                => Task.FromResult(SaveChangesToDeviceAsyncAssert());

            public virtual LoRaADRResult PerformADRAssert() => null;

            public virtual FunctionBundlerResult TryUseBundlerAssert() => null;

            public virtual bool SendDeviceAsyncAssert() => true;

            public virtual Task SendMessageDownstreamAsyncAssert() => null;

            public virtual bool SaveChangesToDeviceAsyncAssert() => true;
        }

        private sealed class DeduplicationTestDataAttribute : Xunit.Sdk.DataAttribute
        {
            private readonly object[] args;

            public DeduplicationTestDataAttribute(string station1,
                string station2,
                DeduplicationMode deduplicationMode,
                int expectedFrameCounterResets,
                int expectedBundlerCalls,
                int expectedFrameCounterDownCalls,
                int expectedMessagesUp,
                int expectedMessagesDown,
                int expectedTwinSaves)
            {
                this.args = new object[] { station1, station2, deduplicationMode, expectedFrameCounterResets, expectedBundlerCalls, expectedFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves };
            }

            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return this.args;
            }
        }

        public ConcentratorDeduplicationDataMessagesIntegrationTests()
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

            this.simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            this.loraDevice = new LoRaDevice(this.simulatedDevice.DevAddr, this.simulatedDevice.DevEUI, ConnectionManager);

            _ = this.frameCounterStrategyMock.Setup(x => x.NextFcntDown(this.loraDevice, It.IsAny<uint>())).Returns(() => ValueTask.FromResult<uint>(1));
            _ = this.frameCounterProviderMock.Setup(x => x.GetStrategy(this.loraDevice.GatewayID)).Returns(this.frameCounterStrategyMock.Object);
        }

        #region UnconfirmedDataMessage
        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // resubmission for unconfirmed messages should not happen anyway but we allow the first message to pass through due to IsABPRelaxedFrameCounter
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // ditto
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // ditto
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        public async Task When_Unconfirmed_First_Data_Message_Test_All_Different_DeduplicationModes(
            string station1,
            string station2,
            DeduplicationMode deduplicationMode,
            int expectedNumberOfFrameCounterResets,
            int expectedNumberOfBundlerCalls,
            int expectedNumberOfFrameCounterDownCalls,
            int expectedMessagesUp,
            int expectedMessagesDown,
            int expectedTwinSaves)
        {
            var dataPayload = this.simulatedDevice.CreateUnconfirmedDataUpMessage("payload");

            await ArrangeActAndAssert(dataPayload, station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // resubmission for unconfirmed message should not happen anyway but it is still dropped as invalid
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // ditto
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // ditto
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        public async Task When_Unconfirmed_Subsequent_Data_Message_Test_All_Different_DeduplicationModes(
            string station1,
            string station2,
            DeduplicationMode deduplicationMode,
            int expectedNumberOfFrameCounterResets,
            int expectedNumberOfBundlerCalls,
            int expectedNumberOfFrameCounterDownCalls,
            int expectedMessagesUp,
            int expectedMessagesDown,
            int expectedTwinSaves)
        {
            var dataPayload = this.simulatedDevice.CreateUnconfirmedDataUpMessage("payload", 10);

            await ArrangeActAndAssert(dataPayload, station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }
        #endregion

        #region ConfirmedDataMessage                                                                                                       
        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 1, expectedMessagesDown: 2, expectedTwinSaves: 2)] // resubmission with drop
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 2, expectedMessagesDown: 2, expectedTwinSaves: 2)] // resubmission
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 2, expectedMessagesDown: 2, expectedTwinSaves: 2)] // resubmission
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 1, expectedMessagesDown: 1, expectedTwinSaves: 1)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 2, expectedMessagesDown: 1, expectedTwinSaves: 2)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 2, expectedMessagesDown: 1, expectedTwinSaves: 2)] // soft duplicate
        public async Task When_First_Confirmed_Data_Message_Test_All_Different_DeduplicationModes(
            string station1,
            string station2,
            DeduplicationMode deduplicationMode,
            int expectedNumberOfFrameCounterResets,
            int expectedNumberOfBundlerCalls,
            int expectedNumberOfFrameCounterDownCalls,
            int expectedMessagesUp,
            int expectedMessagesDown,
            int expectedTwinSaves)
        {
            var dataPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("payload");

            await ArrangeActAndAssert(dataPayload, station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 1, expectedMessagesDown: 2, expectedTwinSaves: 2)] // resubmission with drop
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 2, expectedMessagesDown: 2, expectedTwinSaves: 2)] // resubmission
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 2, expectedMessagesDown: 2, expectedTwinSaves: 2)] // resubmission
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 1, expectedMessagesDown: 1, expectedTwinSaves: 1)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 2, expectedMessagesDown: 1, expectedTwinSaves: 2)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 2, expectedMessagesDown: 1, expectedTwinSaves: 2)] // soft duplicate
        public async Task When_Subsequent_Confirmed_Data_Message_Test_All_Different_DeduplicationModes(
          string station1,
          string station2,
          DeduplicationMode deduplicationMode,
          int expectedNumberOfFrameCounterResets,
          int expectedNumberOfBundlerCalls,
          int expectedNumberOfFrameCounterDownCalls,
          int expectedMessagesUp,
          int expectedMessagesDown,
          int expectedTwinSaves)
        {
            var dataPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("payload", 10);

            await ArrangeActAndAssert(dataPayload, station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }
        #endregion

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 2, 1, 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 1, 1, 1)]
        public async Task When_SingleGateway_Deduplication_Should_Work_The_Same_Way(
            string station1,
            string station2,
            int expectedNumberOfBundlerCalls,
            int expectedMessagesUp,
            int expectedMessagesDown)
        {
            // arrange
            var dataPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("payload", 10);
            var (request1, request2) = SetupRequests(dataPayload, station1, station2);

            var gwId = "foo";
            this.loraDevice.GatewayID = gwId;
            _ = this.frameCounterProviderMock.Setup(x => x.GetStrategy(gwId)).Returns(this.frameCounterStrategyMock.Object);
            this.loraDevice.Deduplication = DeduplicationMode.Drop; // default
            this.loraDevice.NwkSKey = station1;

            // act/assert
            await ActAndAssert(request1, request2, this.loraDevice, null, expectedNumberOfBundlerCalls, null, expectedMessagesUp, expectedMessagesDown, null);
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 1)]
        public async Task When_ADR_Enabled_PerformADR_Should_Be_Disabled_For_SoftDuplicate(
            string station1,
            string station2,
            int expectedNumberOfADRCalls)
        {
            // arrange
            var dataPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("payload");
            dataPayload.FrameControlFlags = FrameControlFlags.Adr; // adr enabled
            var (request1, request2) = SetupRequests(dataPayload, station1, station2);

            this.loraDevice.Deduplication = DeduplicationMode.Mark; // or None
            this.loraDevice.NwkSKey = station1;

            // act/assert
            await ActAndAssert(request1, request2, this.loraDevice);
            this.dataRequestHandlerMock.Verify(x => x.PerformADRAssert(), Times.Exactly(expectedNumberOfADRCalls));
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 0)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 0)]
        public async Task When_Bundler_Returns_FrameCounterDown_Should_Skip_Call_To_FrameCounterDown_Independently_Of_Deduplication_Result(
            string station1,
            string station2,
            int expectedNumberOfFrameCounterDownCalls)
        {
            // arrange
            var dataPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("payload");

            var (request1, request2) = SetupRequests(dataPayload, station1, station2);

            this.loraDevice.Deduplication = DeduplicationMode.Mark; // or Drop or None
            this.loraDevice.NwkSKey = station1;

            _ = this.dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                { IsDuplicate = false, CanProcess = true },
                NextFCntDown = 1
            });

            // act/assert
            await ActAndAssert(request1, request2, this.loraDevice, expectedFrameCounterDownCalls: expectedNumberOfFrameCounterDownCalls);
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 1)]
        public async Task When_Mac_Command_Should_Not_Send_Upstream_Messages_And_Should_Skip_Calls_To_FrameCounterDown_When_SoftDuplicate(
            string station1,
            string station2,
            int expectedNumberOfFrameCounterDownCalls)
        {
            // arrange
            var dataPayload = this.simulatedDevice.CreateUnconfirmedDataUpMessage("payload");
            // MAC command
            dataPayload.Fport = new byte[1] { 0 };
            dataPayload.MacCommands = new List<MacCommand> { new LinkCheckAnswer(1, 1) };

            var (request1, request2) = SetupRequests(dataPayload, station1, station2);

            this.loraDevice.Deduplication = DeduplicationMode.None; // or Mark
            this.loraDevice.NwkSKey = station1;

            // act/assert
            await ActAndAssert(request1, request2, this.loraDevice, expectedFrameCounterDownCalls: expectedNumberOfFrameCounterDownCalls, expectedMessagesUp: 0);
        }

        private async Task ArrangeActAndAssert(
            LoRaPayloadData dataPayload,
            string station1,
            string station2,
            DeduplicationMode deduplicationMode,
            int expectedNumberOfFrameCounterResets,
            int expectedNumberOfBundlerCalls,
            int expectedNumberOfFrameCounterDownCalls,
            int expectedMessagesUp,
            int expectedMessagesDown,
            int expectedTwinSaves)
        {
            var (request1, request2) = SetupRequests(dataPayload, station1, station2);

            this.loraDevice.Deduplication = deduplicationMode;
            this.loraDevice.NwkSKey = station1;

            await ActAndAssert(request1, request2, this.loraDevice, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        private (LoRaRequest request1, LoRaRequest request2) SetupRequests(LoRaPayloadData dataPayload, string station1, string station2)
        {
            return (CreateRequest(station1), CreateRequest(station2));

            WaitableLoRaRequest CreateRequest(string stationEui)
            {
                var loraRequest = CreateWaitableRequest(dataPayload.SerializeUplink(this.simulatedDevice.AppSKey, this.simulatedDevice.NwkSKey).Rxpk[0]);
                loraRequest.SetStationEui(StationEui.Parse(stationEui));
                loraRequest.SetPayload(dataPayload);
                return loraRequest;
            }
        }

        private async Task ActAndAssert(
            LoRaRequest request1,
            LoRaRequest request2,
            LoRaDevice device,
            int? expectedFrameCounterResets = null,
            int? expectedBundlerCalls = null,
            int? expectedFrameCounterDownCalls = null,
            int? expectedMessagesUp = null,
            int? expectedMessagesDown = null,
            int? expectedTwinSaves = null)
        {
            // first request
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request1, device);

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request2, device);

            // assert
            if (expectedFrameCounterResets is int frameCounterResets)
                this.frameCounterStrategyMock.Verify(x => x.ResetAsync(device, It.IsAny<uint>(), ServerGatewayID), Times.Exactly(frameCounterResets));
            if (expectedFrameCounterDownCalls is int frameCounterDownCalls)
                this.frameCounterStrategyMock.Verify(x => x.NextFcntDown(device, It.IsAny<uint>()), Times.Exactly(frameCounterDownCalls));
            if (expectedBundlerCalls is int bundlerCalls)
                this.dataRequestHandlerMock.Verify(x => x.TryUseBundlerAssert(), Times.Exactly(bundlerCalls));
            if (expectedMessagesUp is int messagesUp)
                this.dataRequestHandlerMock.Verify(x => x.SendDeviceAsyncAssert(), Times.Exactly(messagesUp));
            if (expectedMessagesDown is int messagesDown)
                this.dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(), Times.Exactly(messagesDown));
            if (expectedTwinSaves is int twinSaves)
                this.dataRequestHandlerMock.Verify(x => x.SaveChangesToDeviceAsyncAssert(), Times.Exactly(twinSaves));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.cache.Dispose();
                this.loraDevice.Dispose();
            }
        }
    }
}
