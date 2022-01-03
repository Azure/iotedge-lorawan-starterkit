// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Mac;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class DwellTimeIntegrationTests : MessageProcessorTestBase
    {
        private static readonly DwellTimeSetting DesiredDwellTimeSetting = new DwellTimeSetting(false, false, 3);
        private static readonly DwellTimeLimitedRegion As923 = new RegionAS923 { DesiredDwellTimeSetting = DesiredDwellTimeSetting };
        private readonly Mock<TestDefaultLoRaRequestHandler> dataRequestHandlerMock;
        private readonly SimulatedDevice simulatedDevice;
        private readonly LoRaDevice loRaDevice;

        public DwellTimeIntegrationTests()
        {
            this.dataRequestHandlerMock = new Mock<TestDefaultLoRaRequestHandler>(MockBehavior.Default,
                ServerConfiguration,
                FrameCounterUpdateStrategyProvider,
                ConcentratorDeduplication,
                PayloadDecoder,
                new DeduplicationStrategyFactory(NullLoggerFactory.Instance, NullLogger<DeduplicationStrategyFactory>.Instance),
                new LoRaADRStrategyProvider(NullLoggerFactory.Instance),
                new LoRAADRManagerFactory(LoRaDeviceApi.Object, NullLoggerFactory.Instance),
                new FunctionBundlerProvider(LoRaDeviceApi.Object, NullLoggerFactory.Instance, NullLogger<FunctionBundlerProvider>.Instance))
            { CallBase = true };

            this.simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            this.loRaDevice = CreateLoRaDevice(this.simulatedDevice, registerConnection: false);

            LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                         .ReturnsAsync(true);
        }

        public static TheoryData<DwellTimeSetting, DwellTimeSetting?> Sends_TxParamSetupReq_TheoryData() =>
            TheoryDataFactory.From(new (DwellTimeSetting, DwellTimeSetting?)[]
        {
            (new DwellTimeSetting(false, false, 4), new DwellTimeSetting(true, true, 5)),
            (new DwellTimeSetting(false, false, 4), null)
        });

        [Theory]
        [MemberData(nameof(Sends_TxParamSetupReq_TheoryData))]
        public async Task Sends_TxParamSetupReq(DwellTimeSetting desired, DwellTimeSetting? reported)
        {
            // arrange
            var region = new RegionAS923 { DesiredDwellTimeSetting = desired };
            using var request = SetupRequest(region, reported);
            LoRaDeviceApi.Setup(api => api.NextFCntDownAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                         .Returns(Task.FromResult<uint>(1));

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request, this.loRaDevice);

            // assert
            var actualMacCommand = Assert.Single(this.dataRequestHandlerMock.Object.ActualCloudToDeviceMessage.MacCommands);
            Assert.Equal(new TxParamSetupRequest(desired).ToBytes(), actualMacCommand.ToBytes());
            this.dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(It.IsAny<DownlinkMessageBuilderResponse>()), Times.Once);
        }

        public static TheoryData<Region, DwellTimeSetting?> Does_Not_Send_TxParamSetupReq_TheoryData() =>
            TheoryDataFactory.From(new (Region, DwellTimeSetting?)[]
        {
            (new RegionAS923 { DesiredDwellTimeSetting = DesiredDwellTimeSetting }, DesiredDwellTimeSetting),
            (new RegionEU868(), null),
            (new RegionEU868(), new DwellTimeSetting(false, true, 1))
        });

        [Theory]
        [MemberData(nameof(Does_Not_Send_TxParamSetupReq_TheoryData))]
        public async Task Does_Not_Send_TxParamSetupReq(Region region, DwellTimeSetting? reported)
        {
            // arrange
            using var request = SetupRequest(region, reported);
            LoRaDeviceApi.Setup(api => api.NextFCntDownAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                         .Returns(Task.FromResult<uint>(1));

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request, this.loRaDevice);

            // assert
            Assert.Null(this.dataRequestHandlerMock.Object.ActualCloudToDeviceMessage);
            this.dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(It.IsAny<DownlinkMessageBuilderResponse>()), Times.Never);
        }

        public static TheoryData<DwellTimeSetting?> Persists_Reported_Dwell_Time_On_TxParamSetupAns_TheoryData() =>
            TheoryDataFactory.From(new DwellTimeSetting?[]
            {
                null, new DwellTimeSetting(true, false, 9)
            });

        [Theory]
        [MemberData(nameof(Persists_Reported_Dwell_Time_On_TxParamSetupAns_TheoryData))]
        public async Task Persists_Reported_Dwell_Time_On_TxParamSetupAns(DwellTimeSetting? reported)
        {
            // arrange
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>();
            ConnectionManager.Register(this.loRaDevice, loRaDeviceClient.Object);
            TwinCollection? actualTwinCollection = null;
            loRaDeviceClient.Setup(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()))
                            .Callback((TwinCollection t) => actualTwinCollection = t)
                            .ReturnsAsync(true);
            using var request = SetupRequest(As923, reported, new TxParamSetupAnswer());

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request, this.loRaDevice);

            // assert
            loRaDeviceClient.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Once);
            Assert.NotNull(actualTwinCollection);
            var actualDwellTimeSetting = JsonSerializer.Deserialize<DwellTimeSetting>((string)actualTwinCollection![TwinProperty.TxParam].ToString());
            Assert.Equal(As923.DesiredDwellTimeSetting, actualDwellTimeSetting);
            this.dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(It.IsAny<DownlinkMessageBuilderResponse>()), Times.Never);
        }

        public static TheoryData<Region, DwellTimeSetting?> Does_Not_Persist_Reported_Dwell_Time_On_TxParamSetupAns_TheoryData() =>
            TheoryDataFactory.From(new (Region, DwellTimeSetting?)[]
            {
                (As923, DesiredDwellTimeSetting),
                (new RegionEU868(), new DwellTimeSetting(true, false, 9))
            });

        [Fact]
        public async Task When_Reported_Dwell_Time_Already_Persisted_Does_Not_Persist_On_TxParamSetupAns()
        {
            // arrange
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>();
            ConnectionManager.Register(this.loRaDevice, loRaDeviceClient.Object);
            using var request = SetupRequest(As923, DesiredDwellTimeSetting, new TxParamSetupAnswer());

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request, this.loRaDevice);

            // assert
            loRaDeviceClient.Verify(c => c.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Never);
            this.dataRequestHandlerMock.Verify(x => x.SendMessageDownstreamAsyncAssert(It.IsAny<DownlinkMessageBuilderResponse>()), Times.Never);
        }

        [Fact]
        public async Task When_Receiving_TxParamSetupAns_In_Unsupported_Region_Does_Not_Throw()
        {
            // arrange
            using var request = SetupRequest(new RegionEU868(), null, new TxParamSetupAnswer());

            // act + assert
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request, this.loRaDevice);
        }

        public static TheoryData<Region, DwellTimeSetting?, DataRateIndex, DataRateIndex> Uses_Correct_Data_Rate_TheoryData() =>
            TheoryDataFactory.From<Region, DwellTimeSetting?, DataRateIndex, DataRateIndex>(new (Region, DwellTimeSetting?, DataRateIndex, DataRateIndex)[]
            {
                (As923, null, DataRateIndex.DR0, DataRateIndex.DR2),
                (new RegionEU868(), null, DataRateIndex.DR0, DataRateIndex.DR0),
                (As923, new DwellTimeSetting(false, true, 10), DataRateIndex.DR0, DataRateIndex.DR0),
                (As923, new DwellTimeSetting(true, false, 10), DataRateIndex.DR0, DataRateIndex.DR2)
            });

        [Theory]
        [MemberData(nameof(Uses_Correct_Data_Rate_TheoryData))]
        public async Task Uses_Correct_Data_Rate(Region region, DwellTimeSetting? reportedDwellTimeSetting, DataRateIndex upstreamDataRate, DataRateIndex expectedDataRate)
        {
            // arrange
            using var request = SetupRequest(region, reportedDwellTimeSetting, createConfirmed: true, dataRateIndex: upstreamDataRate);
            LoRaDeviceApi.Setup(api => api.NextFCntDownAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                         .Returns(Task.FromResult<uint>(1));

            // act
            _ = await this.dataRequestHandlerMock.Object.ProcessRequestAsync(request, this.loRaDevice);

            // assert
            this.dataRequestHandlerMock.Verify(x =>
                x.SendMessageDownstreamAsyncAssert(
                    It.Is<DownlinkMessageBuilderResponse>(res => res.DownlinkMessage.DataRateRx1 == expectedDataRate)),
                    Times.Once);
        }

        private WaitableLoRaRequest SetupRequest(Region region, DwellTimeSetting? reportedDwellTimeSetting, bool createConfirmed = false, DataRateIndex dataRateIndex = DataRateIndex.DR0) =>
            SetupRequest(region, reportedDwellTimeSetting, null, createConfirmed, dataRateIndex: dataRateIndex);

        private WaitableLoRaRequest SetupRequest(Region region, DwellTimeSetting? reportedDwellTimeSetting, MacCommand? macCommand, bool createConfirmed = false, DataRateIndex dataRateIndex = DataRateIndex.DR0)
        {
            LoRaPayloadData payload;
            if (macCommand is { } someMacCommand)
            {
                payload = this.simulatedDevice.CreateUnconfirmedDataUpMessage(((int)someMacCommand.Cid).ToString("X2", CultureInfo.InvariantCulture), fcnt: 1,
                                                                              fport: 0, isHexPayload: true);
            }
            else if (createConfirmed)
            {
                payload = this.simulatedDevice.CreateConfirmedDataUpMessage("foo");
            }
            else
            {
                payload = this.simulatedDevice.CreateUnconfirmedDataUpMessage("foo");
            }

            // ensure that frequency is within allowed range.
            var freq = new Hertz(region.RegionLimits.FrequencyRange.Min.AsUInt64 + 100);
            var radioMetadata = new RadioMetadata(dataRateIndex, freq, new RadioMetadataUpInfo(0, 0, 0, 0, 0));
            var result = CreateWaitableRequest(radioMetadata, payload);
            result.SetRegion(region);
            this.loRaDevice.UpdateDwellTimeSetting(reportedDwellTimeSetting, acceptChanges: true);
            return result;
        }
    }
}
