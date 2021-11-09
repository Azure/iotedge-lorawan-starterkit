// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication concentratorDeduplication;
        private static readonly UpstreamDataFrame defaultUpdf = new UpstreamDataFrame(default, new DevAddr(1), default, 2, default, default, "payload", new Mic(4), default);


        public ConcentratorDeduplicationTest()
        {
            this.concentratorDeduplication = new ConcentratorDeduplication(NullLogger<IConcentratorDeduplication>.Instance);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            var stationEui = new StationEui();
            if (!isCacheEmpty)
            {
                _ = this.concentratorDeduplication.ShouldDrop(new UpstreamDataFrame(default, default, default, 1, "another_payload", default, default, default, default), stationEui);
            }

            // act
            var result = this.concentratorDeduplication.ShouldDrop(defaultUpdf, stationEui);

            // assert
            Assert.False(result);
            var key = ConcentratorDeduplication.CreateCacheKey(defaultUpdf);
            Assert.True(this.concentratorDeduplication.Cache.TryGetValue(key, out var addedStation));
            Assert.Equal(stationEui, addedStation);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void When_Message_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool sameStationAsBefore, bool expectedResult)
        {
            // arrange
            var stationEui = new StationEui();
            _ = this.concentratorDeduplication.ShouldDrop(defaultUpdf, stationEui);

            var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1234);

            // act/assert
            Assert.Equal(expectedResult, this.concentratorDeduplication.ShouldDrop(defaultUpdf, anotherStation));
            var key = ConcentratorDeduplication.CreateCacheKey(defaultUpdf);
            Assert.Equal(1, this.concentratorDeduplication.Cache.Count);
            Assert.True(this.concentratorDeduplication.Cache.TryGetValue(key, out var addedStation));
            Assert.Equal(expectedResult ? stationEui : anotherStation, addedStation);
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key()
        {
            // arrange
            var expectedKey = "79-67-CF-75-00-58-6B-03-A9-30-B1-C9-84-82-E0-9F-9C-D3-1E-6F-C5-38-9E-D3-F1-63-E6-89-4D-4D-88-B4";

            // act/assert
            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(defaultUpdf));
        }

        public void Dispose() => this.concentratorDeduplication.Dispose();
    }
}
