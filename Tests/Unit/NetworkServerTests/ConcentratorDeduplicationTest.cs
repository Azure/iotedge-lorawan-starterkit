// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Moq;
    using System;
    using System.Net.WebSockets;
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
            // arrange
            var updf = new UpstreamDataFrame(default, 1, "payload", default);
            var stationEui = new StationEui();
            if (!isCacheEmpty)
            {
                _ = this.ConcentratorDeduplication.IsDuplicate(new UpstreamDataFrame(default, 1, "another_payload", default), stationEui);
            }

            // act
            var result = this.ConcentratorDeduplication.IsDuplicate(updf, stationEui);

            // assert
            Assert.False(result);
            var key = ConcentratorDeduplication.CreateCacheKey(updf);
            Assert.True(this.ConcentratorDeduplication.Cache.TryGetValue(key, out var _));
        }

        [Theory]
        [InlineData(true, true, false)] // true, false does not make sense: since we just received this message the socket should still be open
        [InlineData(false, true, true)]
        [InlineData(false, false, false)]
        public void When_Message_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool sameStationAsBefore, bool activeConnection, bool expectedResult)
        {
            // arrange
            var socketMock = new Mock<WebSocket>();
            IWebSocketWriter<string> channel = null;
            if (!activeConnection)
            {
                socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
            }
            channel = new WebSocketTextChannel(socketMock.Object, TimeSpan.FromMinutes(1)); // send timeout not relevant

            var updf = new UpstreamDataFrame(default, 1, "payload", default);
            var stationEui = new StationEui();
            this.ConcentratorDeduplication.IsDuplicate(updf, stationEui);

            this.ConcentratorDeduplication.webSocketRegistry.Add(stationEui, channel);
            var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1234);

            // act/assert
            Assert.Equal(expectedResult, this.ConcentratorDeduplication.IsDuplicate(updf, anotherStation));
            var key = ConcentratorDeduplication.CreateCacheKey(updf);
            Assert.Equal(1, this.ConcentratorDeduplication.Cache.Count);
            this.ConcentratorDeduplication.Cache.TryGetValue(key, out var value);

            if (expectedResult)
            {
                Assert.Equal(value, stationEui);
            }
            else
            {
                Assert.Equal(value, anotherStation);
            }
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
