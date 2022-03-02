// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Common;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;
    using static ReceiveWindowNumber;

    public sealed class ConcentratorDeduplicationDataMessagesIntegrationTests : MessageProcessorTestBase
    {
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategy> frameCounterStrategyMock;
        private readonly Mock<TestDefaultLoRaRequestHandler> dataRequestHandlerMock;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategyProvider> frameCounterProviderMock;
        private readonly MemoryCache cache;
        private readonly SimulatedDevice simulatedABPDevice;
        private readonly LoRaDevice loraABPDevice;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;

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

        public ConcentratorDeduplicationDataMessagesIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
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

            this.dataRequestHandlerMock
                .Setup(d => d.DownlinkMessageBuilderResponseAssert(It.IsAny<LoRaRequest>(),
                                                                   It.IsAny<LoRaDevice>(),
                                                                   It.IsAny<LoRaOperationTimeWatcher>(),
                                                                   It.IsAny<LoRaADRResult>(),
                                                                   It.IsAny<IReceivedLoRaCloudToDeviceMessage>(),
                                                                   It.IsAny<uint?>(),
                                                                   It.IsAny<bool>()))
                .Returns((LoRaRequest request, LoRaDevice device, LoRaOperationTimeWatcher _, LoRaADRResult _, IReceivedLoRaCloudToDeviceMessage _, uint? _, bool _) => new
                             DownlinkMessageBuilderResponse(new DownlinkMessage(((LoRaPayloadData)request.Payload).Serialize(device.AppSKey.Value, device.NwkSKey.Value), default, default, default, default, default, default, default, default),
                                                            isMessageTooLong: false, ReceiveWindow1));

            _ = this.dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                { IsDuplicate = false, CanProcess = true },
                NextFCntDown = null
            });

            this.simulatedABPDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            this.loraABPDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, this.simulatedABPDevice.DevEUI, ConnectionManager)
            {
                AppSKey = this.simulatedABPDevice.AppSKey,
                NwkSKey = this.simulatedABPDevice.NwkSKey
            };

            _ = this.frameCounterStrategyMock.Setup(x => x.NextFcntDown(this.loraABPDevice, It.IsAny<uint>())).Returns(() => ValueTask.FromResult<uint>(1));
            _ = this.frameCounterProviderMock.Setup(x => x.GetStrategy(this.loraABPDevice.GatewayID)).Returns(this.frameCounterStrategyMock.Object);
        }

        #region UnconfirmedDataMessage
        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 4)] // resubmission for unconfirmed first messages can happen when the device was reset + sends the same payload within the cache retention timewindow,
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 4)] // [cont] we have no reliable way of knowing that
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 4)] // [cont] so deduplication here is on a best-effort case
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 2)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 4)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 4)] // soft duplicate
        public async Task When_First_Unconfirmed_Data_Message_Test_All_Different_DeduplicationModes(
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
            await ArrangeActAndAssert(() => this.simulatedABPDevice.CreateUnconfirmedDataUpMessage("payload", fcnt: 1),
                                      station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // resubmission for unconfirmed subsequent messages is even more unlikely (compared to first messages) so messages are dropped as invalid
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // ditto
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // ditto
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        public async Task When_Subsequent_Unconfirmed_Data_Message_Test_All_Different_DeduplicationModes(
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
            await ArrangeActAndAssert(() => this.simulatedABPDevice.CreateUnconfirmedDataUpMessage("payload", 10),
                                      station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }
        #endregion

        #region ConfirmedDataMessage
        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 1, expectedMessagesDown: 2, expectedTwinSaves: 4)] // resubmission with drop
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 2, expectedMessagesDown: 2, expectedTwinSaves: 4)] // resubmission
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 2, expectedFrameCounterDownCalls: 2, expectedMessagesUp: 2, expectedMessagesDown: 2, expectedTwinSaves: 4)] // resubmission
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 1, expectedMessagesDown: 1, expectedTwinSaves: 2)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 2, expectedMessagesDown: 1, expectedTwinSaves: 4)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 1, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 1, expectedMessagesUp: 2, expectedMessagesDown: 1, expectedTwinSaves: 4)] // soft duplicate
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
            await ArrangeActAndAssert(() => this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload", fcnt: 1),
                                      station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
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
            await ArrangeActAndAssert(() => this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload", fcnt: 10),
                                      station1, station2, deduplicationMode, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }
        #endregion

        [Theory]
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // resubmission for unconfirmed message should not happen anyway but it is still dropped as invalid
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // resubmission for unconfirmed message should not happen anyway but it is still dropped as invalid
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // resubmission for unconfirmed message should not happen anyway but it is still dropped as invalid
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 1, expectedMessagesDown: 0, expectedTwinSaves: 1)] // duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        [DeduplicationTestData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, expectedFrameCounterResets: 0, expectedBundlerCalls: 1, expectedFrameCounterDownCalls: 0, expectedMessagesUp: 2, expectedMessagesDown: 0, expectedTwinSaves: 2)] // soft duplicate
        public async Task When_First_Unconfirmed_Data_Message_With_OTAA_Device_Test_All_Different_DeduplicationModes(
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

            var value32 = "00000000000000000000000000000000";
            var simulatedOTAADevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0)) { DevAddr = new DevAddr(0) };

            var dataPayload = simulatedOTAADevice.CreateUnconfirmedDataUpMessage("payload", appSKey: AppSessionKey.Parse(value32), nwkSKey: NetworkSessionKey.Parse(value32));
            var request1 = CreateOTAARequest(dataPayload, station1);
            var request2 = CreateOTAARequest(dataPayload, station2);

            await using var loraOTAADevice = new LoRaDevice(simulatedOTAADevice.DevAddr, simulatedOTAADevice.DevEUI, ConnectionManager);
            loraOTAADevice.AppKey = AppKey.Parse(value32);

            loraOTAADevice.Deduplication = deduplicationMode;
            loraOTAADevice.NwkSKey = TestKeys.CreateNetworkSessionKey(1);
            loraOTAADevice.AppSKey = TestKeys.CreateAppSessionKey(2);

            _ = this.frameCounterStrategyMock.Setup(x => x.NextFcntDown(loraOTAADevice, It.IsAny<uint>())).Returns(() => ValueTask.FromResult<uint>(1));

            await ActAndAssert(new[] { request1, request2 }, loraOTAADevice, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);

            LoRaRequest CreateOTAARequest(LoRaPayloadData payload, string station)
            {
                var request = CreateWaitableRequest(payload);
                request.SetStationEui(StationEui.Parse(station));
                request.SetPayload(payload);
                return request;
            }
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", 0, 2, 2)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", 0, 2, 1)]
        public async Task When_SingleGateway_Deduplication_Should_Work_The_Same_Way(
            string station1,
            string station2,
            int expectedNumberOfBundlerCalls,
            int expectedMessagesUp,
            int expectedMessagesDown)
        {
            // arrange
            var requests =
                SetupRequests(() => this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload", fcnt: 10),
                              new[] { station1, station2 });

            var gwId = "foo";
            this.loraABPDevice.GatewayID = gwId;
            _ = this.frameCounterProviderMock.Setup(x => x.GetStrategy(gwId)).Returns(this.frameCounterStrategyMock.Object);
            this.loraABPDevice.Deduplication = DeduplicationMode.None; // default
            this.loraABPDevice.NwkSKey = TestKeys.CreateNetworkSessionKey(1);
            this.loraABPDevice.AppSKey = TestKeys.CreateAppSessionKey(2);

            // act/assert
            await ActAndAssert(requests, this.loraABPDevice, null, expectedNumberOfBundlerCalls, null, expectedMessagesUp, expectedMessagesDown, null);
        }

        [Theory]
        [InlineData(new[] { "11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", "33-33-33-33-33-33-33-33", "44-44-44-44-44-44-44-44", "55-55-55-55-55-55-55-55" }, DeduplicationMode.Mark, 5, 1)]
        [InlineData(new[] { "11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", "33-33-33-33-33-33-33-33", "44-44-44-44-44-44-44-44", "55-55-55-55-55-55-55-55" }, DeduplicationMode.None, 5, 1)]
        public async Task When_More_Than_4_Concentrators_Messages_Should_Go_Upstream(
            string[] stations,
            DeduplicationMode mode,
            int expectedMessagesUp,
            int expectedMessagesDown)
        {
            // This test makes sense only if the condition is met (+ 1 is added for the very first message)
            // If not, we fail the test proactively to notify that the test code should be amended. 
            Assert.True(stations.Length > LoRaDevice.MaxConfirmationResubmitCount + 1, "Ensure that the test tests with more stations than LoRaDevice.MaxConfirmationResubmitCount + 1");

            // arrange
            var requests =
                SetupRequests(() => this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload", fcnt: 10),
                              stations);

            this.loraABPDevice.Deduplication = mode;

            // act/assert
            await ActAndAssert(requests, this.loraABPDevice, null, null, null, expectedMessagesUp, expectedMessagesDown, null);
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
            var requests =
                SetupRequests(() => this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload", FrameControlFlags.Adr, fcnt: 10),
                              new[] { station1, station2 });

            this.loraABPDevice.Deduplication = DeduplicationMode.Mark; // or None
            this.loraABPDevice.NwkSKey = TestKeys.CreateNetworkSessionKey(1);
            this.loraABPDevice.AppSKey = TestKeys.CreateAppSessionKey(2);

            // act/assert
            await ActAndAssert(requests, this.loraABPDevice);
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
            var requests =
                SetupRequests(() => this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload"),
                              new[] { station1, station2 });

            this.loraABPDevice.Deduplication = DeduplicationMode.Mark; // or Drop or None
            this.loraABPDevice.NwkSKey = TestKeys.CreateNetworkSessionKey(1);
            this.loraABPDevice.AppSKey = TestKeys.CreateAppSessionKey(2);

            _ = this.dataRequestHandlerMock.Setup(x => x.TryUseBundlerAssert()).Returns(new FunctionBundlerResult
            {
                DeduplicationResult = new DeduplicationResult
                { IsDuplicate = false, CanProcess = true },
                NextFCntDown = 1
            });

            // act/assert
            await ActAndAssert(requests, this.loraABPDevice, expectedFrameCounterDownCalls: expectedNumberOfFrameCounterDownCalls);
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
            LoRaPayloadData CreateDataPayload() =>
                this.simulatedABPDevice.CreateUnconfirmedDataUpMessage(null, fcnt: 1,
                                                                       fport: FramePort.MacCommand,
                                                                       macCommands: new List<MacCommand> { new LinkCheckRequest() });

            var requests = SetupRequests(CreateDataPayload, new[] { station1, station2 });

            this.loraABPDevice.Deduplication = DeduplicationMode.None; // or Mark

            // act/assert
            await ActAndAssert(requests, this.loraABPDevice, expectedFrameCounterDownCalls: expectedNumberOfFrameCounterDownCalls, expectedMessagesUp: 0);
        }

        private async Task ArrangeActAndAssert(
            Func<LoRaPayloadData> dataPayloadFactory,
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
            var requests = SetupRequests(dataPayloadFactory, new[] { station1, station2 });

            this.loraABPDevice.Deduplication = deduplicationMode;

            await ActAndAssert(requests, this.loraABPDevice, expectedNumberOfFrameCounterResets, expectedNumberOfBundlerCalls, expectedNumberOfFrameCounterDownCalls, expectedMessagesUp, expectedMessagesDown, expectedTwinSaves);
        }

        private WaitableLoRaRequest[] SetupRequests(Func<LoRaPayloadData> dataPayloadFactory, string[] stations)
        {
            return stations.Select(st => CreateRequest(st)).ToArray();

            WaitableLoRaRequest CreateRequest(string stationEui)
            {
                var payload = dataPayloadFactory();
                var loraRequest = CreateWaitableRequest(dataPayloadFactory());
                loraRequest.SetStationEui(StationEui.Parse(stationEui));
                loraRequest.SetPayload(payload);
                return loraRequest;
            }
        }

        private async Task ActAndAssert(
            LoRaRequest[] requests,
            LoRaDevice device,
            int? expectedFrameCounterResets = null,
            int? expectedBundlerCalls = null,
            int? expectedFrameCounterDownCalls = null,
            int? expectedMessagesUp = null,
            int? expectedMessagesDown = null,
            int? expectedTwinSaves = null)
        {
            // act sequentially
            foreach (var r in requests)
            {
                _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(r, device);
            }

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
                this.dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(It.IsAny<DownlinkMessageBuilderResponse>()), Times.Exactly(messagesDown));
            if (expectedTwinSaves is int twinSaves)
                this.dataRequestHandlerMock.Verify(x => x.SaveChangesToDeviceAsyncAssert(), Times.Exactly(twinSaves));
        }

        protected override async ValueTask DisposeAsync(bool disposing)
        {
            await base.DisposeAsync(disposing);
            if (disposing)
            {
                this.cache.Dispose();
                this.testOutputLoggerFactory.Dispose();
                await this.loraABPDevice.DisposeAsync();
            }
        }
    }
}
