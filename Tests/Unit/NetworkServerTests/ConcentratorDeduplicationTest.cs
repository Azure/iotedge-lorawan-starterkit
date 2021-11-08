// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication concentratorDeduplication;
        private static readonly UpstreamDataFrame defaultUpdf = new UpstreamDataFrame(default, default, default, 1, "payload", default, default, default, default);


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
        public void CacheKey_Should_Consider_All_Required_Fields()
        {
            // arrange
            var updf = new Mock<UpstreamDataFrame>(
                new MacHeader(),
                default,
                default,
                default,
                default,
                default,
                "payload",
                default,
                default);
            _ = updf.Setup(x => x.DevAddr);
            _ = updf.Setup(x => x.Counter);
            _ = updf.Setup(x => x.Payload);
            _ = updf.Setup(x => x.Mic);

            // act
            _ = ConcentratorDeduplication.CreateCacheKey(updf.Object);

            // assert
            updf.VerifyAll();
        }

        [Theory]
        [InlineData(100, 50, true)]
        [InlineData(100, 150, false)]
        public async void CachedEntries_Should_Expire(int expirationTimeout, int delay, bool expectedResult)
        {
            // arrange
            using var sut = new ConcentratorDeduplication(NullLogger<IConcentratorDeduplication>.Instance, expirationTimeout);
            var stationEui = new StationEui();

            // act
            _ = sut.ShouldDrop(defaultUpdf, stationEui);

            // assert
            await Task.Delay(delay);
            var key = ConcentratorDeduplication.CreateCacheKey(defaultUpdf);
            Assert.Equal(expectedResult, sut.Cache.TryGetValue(key, out var _));
        }

        public void Dispose() => this.concentratorDeduplication.Dispose();
    }
}
