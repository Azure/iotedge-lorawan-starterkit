// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication concentratorDeduplication;
        private static readonly UpstreamDataFrame defaultUpdf = new UpstreamDataFrame(default, new DevAddr(1), default, 2, default, default, "payload", new Mic(4), default);

#pragma warning disable CA2213 // Disposable fields should be disposed
        // false positive, ownership passed to ConcentratorDeduplication
        private readonly MemoryCache cache;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public ConcentratorDeduplicationTest()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.concentratorDeduplication = new ConcentratorDeduplication(
                this.cache,
                NullLogger<IConcentratorDeduplication>.Instance);
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
                _ = this.concentratorDeduplication.ShouldDrop(new UpstreamDataFrame(default, new DevAddr(1), default, 2, default, default, "another_payload", new Mic(4), default), stationEui);
            }

            // act
            var result = this.concentratorDeduplication.ShouldDrop(defaultUpdf, stationEui);

            // assert
            Assert.False(result);
            var key = ConcentratorDeduplication.CreateCacheKey(defaultUpdf);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
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
            Assert.Equal(1, this.cache.Count);
            var key = ConcentratorDeduplication.CreateCacheKey(defaultUpdf);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(expectedResult ? stationEui : anotherStation, addedStation);
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key()
        {
            // arrange
            var expectedKey = "43-E3-69-8D-70-E2-50-77-06-01-63-D1-DD-74-ED-E0-B5-BA-3B-54-09-FB-88-B3-B9-DB-6D-97-68-01-97-52";

            // act/assert
            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(defaultUpdf));
        }

        public void Dispose() => this.concentratorDeduplication.Dispose();
    }
}
