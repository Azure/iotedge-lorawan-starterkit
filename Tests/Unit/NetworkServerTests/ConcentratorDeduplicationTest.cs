// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using System;
    using System.Net.WebSockets;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication ConcentratorDeduplication;

        public ConcentratorDeduplicationTest()
        {
            this.ConcentratorDeduplication = new ConcentratorDeduplication(NullLogger<ConcentratorDeduplication>.Instance);
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
        [InlineData(true, true, true, false)]
        [InlineData(true, false, false, true)]
        [InlineData(false, true, true, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        public void When_Message_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool sameStationAsBefore, bool activeConnectionToPreviousStation, bool activeConnectionToCurrentStation, bool expectedResult)
        {
            // arrange
            var stationEui = new StationEui();
            var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1234);

            var previousStationSocketMock = new Mock<WebSocket>();
            var currentStationSocketMock = new Mock<WebSocket>();

            IWebSocketWriter<string> previousChannel = null;
            IWebSocketWriter<string> currentChannel = null;
            if (!activeConnectionToPreviousStation)
            {
                previousStationSocketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
            }
            if (!activeConnectionToCurrentStation)
            {
                currentStationSocketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
            }
            previousChannel = new WebSocketTextChannel(previousStationSocketMock.Object, TimeSpan.FromMinutes(1)); // send timeout not relevant
            currentChannel = new WebSocketTextChannel(currentStationSocketMock.Object, TimeSpan.FromMinutes(1)); // send timeout not relevant
            this.ConcentratorDeduplication.webSocketRegistry.Add(stationEui, previousChannel);

            if (!sameStationAsBefore)
            {
                this.ConcentratorDeduplication.webSocketRegistry.Add(anotherStation, currentChannel);
            }

            var updf = new UpstreamDataFrame(default, 1, "payload", default);
            this.ConcentratorDeduplication.IsDuplicate(updf, stationEui);


            // act/assert
            Assert.Equal(expectedResult, this.ConcentratorDeduplication.IsDuplicate(updf, anotherStation));
            var key = ConcentratorDeduplication.CreateCacheKey(updf);
            Assert.Equal(1, this.ConcentratorDeduplication.Cache.Count);
            Assert.True(this.ConcentratorDeduplication.Cache.TryGetValue(key, out var _));
        }

        public void Dispose() => this.ConcentratorDeduplication.Dispose();
    }
}
