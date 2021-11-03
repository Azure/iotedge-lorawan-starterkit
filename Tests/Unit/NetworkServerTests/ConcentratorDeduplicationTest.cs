// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using System;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication ConcentratorDeduplication;

        public ConcentratorDeduplicationTest()
        {
            this.ConcentratorDeduplication = new ConcentratorDeduplication();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool isCacheEmpty)
        {
            var updf = new UpstreamDataFrame(default, 1, "payload", default);
            var stationEui = new StationEui();
            if (!isCacheEmpty)
            {
                _ = this.ConcentratorDeduplication.IsDuplicate(new UpstreamDataFrame(default, 1, "another_payload", default), stationEui);
            }

            var result = this.ConcentratorDeduplication.IsDuplicate(updf, stationEui);
            Assert.False(result);

            var key = ConcentratorDeduplication.CreateCacheKey(updf);
            Assert.True(this.ConcentratorDeduplication.Cache.TryGetValue(key, out var _));
        }

        [Theory]
        //[InlineData(true, true)] // true, false does not make sense: since we just received this message the socket should still be open
        [InlineData(false, true)]
        //[InlineData(false, false)]
        public void When_Message_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool sameStationAsBefore, bool activeConnection)
        {
            Console.WriteLine(activeConnection);

            var updf = new UpstreamDataFrame(default, 1, "payload", default);
            var stationEui = new StationEui();
            this.ConcentratorDeduplication.IsDuplicate(updf, stationEui);
            var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1);

            Assert.False(this.ConcentratorDeduplication.IsDuplicate(updf, anotherStation));

            var key = ConcentratorDeduplication.CreateCacheKey(updf);
            this.ConcentratorDeduplication.Cache.TryGetValue(key, out var value);
            Assert.Equal(value, anotherStation);
        }

        public void Dispose() => this.ConcentratorDeduplication.Dispose();
    }
}
