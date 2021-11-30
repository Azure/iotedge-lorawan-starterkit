// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net.WebSockets;
    using Bogus;
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.ObjectPool;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication concentratorDeduplication;
        private readonly LoRaDeviceClientConnectionManager connectionManager;
        private readonly LoRaDevice loRaDevice;
        private readonly SimulatedDevice simulatedDevice;
        private readonly WaitableLoRaRequest loraRequest;
        private readonly WebSocketWriterRegistry<StationEui, string> socketRegistry;

#pragma warning disable CA2213 // Disposable fields should be disposed
        // false positive, ownership passed to ConcentratorDeduplication
        private readonly MemoryCache cache;
        private readonly Mock<DeduplicationStrategyFactory> deduplicationStrategyFactory;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public static class TheoryMembers
        {
            public static IEnumerable<object[]> DeduplicationStrategies()
            {
                yield return new object[] { new DeduplicationStrategyMark(NullLogger<DeduplicationStrategyMark>.Instance) };
                yield return new object[] { null };
            }
        }

        public ConcentratorDeduplicationTest()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);

            this.simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("payload");
            this.loraRequest = WaitableLoRaRequest.Create(dataPayload);
            this.loRaDevice = new LoRaDevice(this.simulatedDevice.DevAddr, this.simulatedDevice.DevEUI, this.connectionManager);

            this.deduplicationStrategyFactory = new Mock<DeduplicationStrategyFactory>(NullLoggerFactory.Instance, NullLogger<DeduplicationStrategyFactory>.Instance);
            this.deduplicationStrategyFactory.Setup(x => x.Create(this.loRaDevice)).Returns(new DeduplicationStrategyDrop(NullLogger<DeduplicationStrategyDrop>.Instance)); ;
            this.socketRegistry = new WebSocketWriterRegistry<StationEui, string>(NullLogger<WebSocketWriterRegistry<StationEui, string>>.Instance);

            this.concentratorDeduplication = new ConcentratorDeduplication(
                this.cache,
                deduplicationStrategyFactory.Object,
                this.socketRegistry,
                NullLogger<IConcentratorDeduplication>.Instance);
        }

        [Fact]
        public void ShouldProcess_JoinRequests()
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            var joinRxpk = simulatedDevice.CreateJoinRequest().SerializeUplink(simulatedDevice.AppKey).Rxpk[0]; ;
            using var loraRequest = WaitableLoRaRequest.Create(joinRxpk);
            loraRequest.SetPayload(new LoRaPayloadJoinRequest());

            // act / assert
            var shouldProcess = this.concentratorDeduplication.ShouldProcess(loraRequest, this.loRaDevice);
            Assert.True(shouldProcess);
        }

        [Theory]
        [MemberData(nameof(TheoryMembers.DeduplicationStrategies), MemberType = typeof(TheoryMembers))]
        public void ShouldNotProcess_Mark_And_None_Deduplication_Strategies(ILoRaDeviceMessageDeduplicationStrategy strategy)
        {
            // arrange
            using var otherDevice = new LoRaDevice(this.simulatedDevice.DevAddr, new Faker().Random.String(10), this.connectionManager);
            this.deduplicationStrategyFactory.Setup(x => x.Create(otherDevice)).Returns(strategy); ;

            using var concentratorDeduplication = new ConcentratorDeduplication(
                this.cache,
                deduplicationStrategyFactory.Object,
                this.socketRegistry,
                NullLogger<IConcentratorDeduplication>.Instance);

            // act / assert
            var shouldNotProcess = concentratorDeduplication.ShouldProcess(this.loraRequest, otherDevice);
            Assert.False(shouldNotProcess);

            // should not affect existing mock
            var shouldProcess = concentratorDeduplication.ShouldProcess(this.loraRequest, this.loRaDevice);
            Assert.True(shouldProcess);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            if (!isCacheEmpty)
            {
                var anotherPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("another_payload");
                using var anotherRequest = WaitableLoRaRequest.Create(anotherPayload);
                _ = this.concentratorDeduplication.ShouldDrop(anotherRequest, this.loRaDevice);
            }

            // act
            var result = this.concentratorDeduplication.ShouldDrop(this.loraRequest, this.loRaDevice);

            // assert
            Assert.False(result);
            var key = ConcentratorDeduplication.CreateCacheKey(this.loraRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(this.loraRequest.StationEui, addedStation);
        }

        [Theory]
        [InlineData(true, true, false)]
        // we consider sameStationAsBefore: true, activeConnectionToPreviousStation: false
        // an edge case that we don't need to cover since we just received a message from that same station
        [InlineData(false, true, true)]
        [InlineData(false, false, false)]
        public void When_Message_Encountered_Should_Not_Find_Duplicates_And_Add_To_Cache(bool sameStationAsBefore, bool activeConnectionToPreviousStation, bool expectedResult)
        {
            // arrange
            var stationEui = this.loraRequest.StationEui;
            _ = this.concentratorDeduplication.ShouldDrop(this.loraRequest, this.loRaDevice);

            var socketMock = new Mock<WebSocket>();
            IWebSocketWriter<string> channel = null;
            if (!activeConnectionToPreviousStation)
            {
                _ = socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
            }
            channel = new WebSocketTextChannel(socketMock.Object, TimeSpan.FromMinutes(1)); // send timeout not relevant
            _ = this.socketRegistry.Register(stationEui, channel);

            var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1234);
            this.loraRequest.SetStationEui(anotherStation);

            // act/assert
            Assert.Equal(expectedResult, this.concentratorDeduplication.ShouldDrop(this.loraRequest, this.loRaDevice));
            Assert.Equal(1, this.cache.Count);
            var key = ConcentratorDeduplication.CreateCacheKey(this.loraRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(expectedResult ? stationEui : anotherStation, addedStation);
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key_For_UpstreamDataFrames()
        {
            // arrange
            var expectedKey = "13-DD-E3-E3-41-1B-29-DF-1C-3F-A8-EF-6D-BB-51-99-84-BD-90-03-B4-FA-5E-3A-ED-C2-0F-3A-5B-6B-80-C5";

            // act/assert
            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(this.loraRequest));
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key_For_JoinRequests()
        {
            // arrange
            var simulatedOTAADevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            var joinPayload = simulatedOTAADevice.CreateJoinRequest();
            joinPayload.DevNonce = new Memory<byte>(new byte[2]);
            this.loraRequest.SetPayload(joinPayload);

            var expectedKey = "60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4";

            // act/assert
            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(this.loraRequest));
        }

        [Fact]
        public void CreateKeyMethod_Should_Throw_When_Used_With_Wrong_Type()
        {
            // arrange
            this.loraRequest.SetPayload(Mock.Of<LoRaPayloadJoinAccept>());

            // act / assert
            Assert.Throws<ArgumentException>(() => ConcentratorDeduplication.CreateCacheKey(this.loraRequest));
        }

        public void Dispose()
        {
            this.loraRequest.Dispose();
            this.loRaDevice.Dispose();

            this.connectionManager.Dispose();
            this.concentratorDeduplication.Dispose();
        }
    }
}
