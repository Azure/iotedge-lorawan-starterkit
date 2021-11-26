// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Text;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.Regions;
    using global::LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private static readonly LoRaRequest defaultRequest;

        private readonly ConcentratorDeduplication concentratorDeduplication;
        private readonly LoRaDeviceClientConnectionManager connectionManager;
        private readonly LoRaDevice loRaDevice;
        private readonly WebSocketWriterRegistry<StationEui, string> socketRegistry;

#pragma warning disable CA2213 // Disposable fields should be disposed
        // false positive, ownership passed to ConcentratorDeduplication
        private readonly MemoryCache cache;
#pragma warning restore CA2213 // Disposable fields should be disposed


#pragma warning disable CA1810 // Initialize reference type static fields inline
        // it's not trivial to initialize LoRaRequest inline
        static ConcentratorDeduplicationTest()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            var TestDataFrame =
                @"{msgtype: 'updf', MHdr: 128, DevAddr: 58772467, FCtrl: 80, FCnt: 164, FOpts: '', FPort: 8, FRMPayload: '5ABBBA', MIC: -1943282916, RefTime: 0.0, DR: 4, Freq: 868100000, upinfo: {rctx: 0, xtime: 40250921680313459, gpstime: 0, fts: -1, rssi: -60, snr: 9, rxtime: 1635347491.917289}}";
            var updf = LnsData.UpstreamDataFrameReader.Read(JsonUtil.Strictify(TestDataFrame));
            var routerRegion = new RegionEU868();

            var rxpk = new BasicStationToRxpk(updf.RadioMetadata, routerRegion);
            var downstreamMock = Mock.Of<DownstreamSender>();
            defaultRequest = new LoRaRequest(rxpk, downstreamMock, DateTime.UtcNow);

            var devAddrText = "58772467";
            var devAddr = ConversionHelper.StringToByteArray(devAddrText);
            Array.Reverse(devAddr);

            ushort fcnt = 164;
            var fcntBytes = BitConverter.GetBytes(fcnt);

            var payload = Encoding.UTF8.GetBytes("5ABBBA");
            Array.Reverse(payload);
            var devicePayloadData = new LoRaPayloadData(LoRaMessageType.ConfirmedDataUp, devAddr, new byte[] { 0x80 }, fcntBytes, default, default, payload, 0); // uplink

            defaultRequest.SetPayload(devicePayloadData);
            defaultRequest.SetRegion(routerRegion);
            defaultRequest.SetStationEui(new StationEui());
        }

        public ConcentratorDeduplicationTest()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
            this.loRaDevice = new LoRaDevice(new DevAddr().ToString(), new DevEui().ToString(), this.connectionManager);

            this.socketRegistry = new WebSocketWriterRegistry<StationEui, string>(NullLogger<WebSocketWriterRegistry<StationEui, string>>.Instance);

            var deduplicationStrategyFactory = new Mock<DeduplicationStrategyFactory>(NullLoggerFactory.Instance, NullLogger<DeduplicationStrategyFactory>.Instance);
            _ = deduplicationStrategyFactory.Setup(x => x.Create(It.IsAny<LoRaDevice>())).Returns(new DeduplicationStrategyDrop(NullLogger<DeduplicationStrategyDrop>.Instance)); ;

            this.concentratorDeduplication = new ConcentratorDeduplication(
                this.cache,
                deduplicationStrategyFactory.Object,
                this.socketRegistry,
                NullLogger<IConcentratorDeduplication>.Instance);
        }

        [Theory]
        [InlineData(true)]
        //[InlineData(false)]
        public void When_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            if (!isCacheEmpty)
            {
                // TODO create a different payload
                _ = this.concentratorDeduplication.ShouldDrop(defaultRequest, this.loRaDevice);
            }

            // act
            var result = this.concentratorDeduplication.ShouldDrop(defaultRequest, this.loRaDevice);

            // assert
            Assert.False(result);
            var key = ConcentratorDeduplication.CreateCacheKey(defaultRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(defaultRequest.StationEui, addedStation);
        }

        //[Theory]
        //[InlineData(true, true, false)]
        //// we consider sameStationAsBefore: true, activeConnectionToPreviousStation: false
        //// an edge case that we don't need to cover since we just received a message from that same station
        //[InlineData(false, true, true)]
        //[InlineData(false, false, false)]
        //public void When_Message_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool sameStationAsBefore, bool activeConnectionToPreviousStation, bool expectedResult)
        //{
        //    // arrange
        //    var stationEui = new StationEui();
        //    _ = this.concentratorDeduplication.ShouldDrop(defaultUpdf, stationEui);

        //    var socketMock = new Mock<WebSocket>();
        //    IWebSocketWriter<string> channel = null;
        //    if (!activeConnectionToPreviousStation)
        //    {
        //        _ = socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
        //    }
        //    channel = new WebSocketTextChannel(socketMock.Object, TimeSpan.FromMinutes(1)); // send timeout not relevant
        //    _ = this.socketRegistry.Register(stationEui, channel);

        //    var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1234);

        //    // act/assert
        //    Assert.Equal(expectedResult, this.concentratorDeduplication.ShouldDrop(defaultUpdf, anotherStation));
        //    Assert.Equal(1, this.cache.Count);
        //    var key = ConcentratorDeduplication<UpstreamDataFrame>.CreateCacheKey(defaultUpdf);
        //    Assert.True(this.cache.TryGetValue(key, out var addedStation));
        //    Assert.Equal(expectedResult ? stationEui : anotherStation, addedStation);
        //}

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key_For_UpstreamDataFrames()
        {
            // arrange
            var expectedKey = "95-D8-37-05-6D-9A-5C-6E-4D-0A-4C-B1-6A-F4-3E-EB-E2-67-A3-5E-20-38-D9-4A-59-96-75-98-77-C5-27-59";

            // act/assert
            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(defaultRequest));
        }

        //[Fact]
        //public void CreateKeyMethod_Should_Produce_Expected_Key_For_JoinRequests()
        //{
        //    // arrange
        //    var joinReq = new JoinRequestFrame(default, default, default, default, default, default);

        //    var expectedKey = "60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4";

        //    // act/assert
        //    Assert.Equal(expectedKey, ConcentratorDeduplication<JoinRequestFrame>.CreateCacheKey(joinReq));
        //}

        [Fact]
        public void CreateKeyMethod_Should_Throw_When_Used_With_Wrong_Type()
        {
            // arrange
            defaultRequest.SetPayload(Mock.Of<LoRaPayloadJoinAccept>());

            // act / assert
            Assert.Throws<ArgumentException>(() => ConcentratorDeduplication.CreateCacheKey(defaultRequest));
        }

        public void Dispose()
        {
            this.connectionManager.Dispose();
            this.loRaDevice.Dispose();
            this.concentratorDeduplication.Dispose();
        }
    }
}
