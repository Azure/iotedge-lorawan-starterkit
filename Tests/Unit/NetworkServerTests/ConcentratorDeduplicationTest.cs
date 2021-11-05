// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using System;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication ConcentratorDeduplication;

        public ConcentratorDeduplicationTest()
        {
            this.ConcentratorDeduplication = new ConcentratorDeduplication(NullLogger<IConcentratorDeduplication>.Instance);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            var updf = new UpstreamDataFrame(default, 1, "payload", default);
            var stationEui = new StationEui();
            if (!isCacheEmpty)
            {
                _ = this.ConcentratorDeduplication.ShouldDrop(new UpstreamDataFrame(default, 1, "another_payload", default), stationEui);
            }

            // act
            var result = this.ConcentratorDeduplication.ShouldDrop(updf, stationEui);

            // assert
            Assert.False(result);
            var key = ConcentratorDeduplication.CreateCacheKey(updf);
            Assert.True(this.ConcentratorDeduplication.Cache.TryGetValue(key, out var addedStation));
            Assert.Equal(stationEui, addedStation);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void When_Message_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool sameStationAsBefore, bool expectedResult)
        {
            // arrange
            var updf = new UpstreamDataFrame(default, 1, "payload", default);
            var stationEui = new StationEui();
            _ = this.ConcentratorDeduplication.ShouldDrop(updf, stationEui);

            var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1234);

            // act/assert
            Assert.Equal(expectedResult, this.ConcentratorDeduplication.ShouldDrop(updf, anotherStation));
            var key = ConcentratorDeduplication.CreateCacheKey(updf);
            Assert.Equal(1, this.ConcentratorDeduplication.Cache.Count);
            Assert.True(this.ConcentratorDeduplication.Cache.TryGetValue(key, out var addedStation));
            Assert.Equal(expectedResult ? stationEui : anotherStation, addedStation);
        }

        [Fact]
        public void CacheKey_Should_Consider_All_Required_Fields()
        {
            // arrange
            var updf = new Mock<UpstreamDataFrame>(
                new DevAddr(0x0),
                (ushort)0x0,
                "payload",
                new Mic(0x0));
            updf.Setup(x => x.DevAddr);
            updf.Setup(x => x.FrameCounter);
            updf.Setup(x => x.FRMPayload);
            updf.Setup(x => x.Mic);

            // act
            _ = ConcentratorDeduplication.CreateCacheKey(updf.Object);

            // assert
            updf.VerifyAll();
        }

        public void Dispose() => this.ConcentratorDeduplication.Dispose();
    }
}
