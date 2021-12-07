// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication concentratorDeduplication;
        private readonly LoRaDeviceClientConnectionManager connectionManager;
        private readonly LoRaDevice loRaDevice;
        private readonly SimulatedDevice simulatedDevice;
        private readonly WaitableLoRaRequest loraRequest;
        private readonly MemoryCache cache; // ownership passed to ConcentratorDeduplication

        public ConcentratorDeduplicationTest()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);

            this.simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("payload");
            this.loraRequest = WaitableLoRaRequest.Create(dataPayload);
            this.loRaDevice = new LoRaDevice(this.simulatedDevice.DevAddr, this.simulatedDevice.DevEUI, this.connectionManager)
            {
                Deduplication = DeduplicationMode.Drop // the default
            };

            this.concentratorDeduplication = new ConcentratorDeduplication(
                this.cache,
                NullLogger<IConcentratorDeduplication>.Instance);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Should_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            if (!isCacheEmpty)
            {
                var anotherPayload = this.simulatedDevice.CreateConfirmedDataUpMessage("another_payload");
                using var anotherRequest = WaitableLoRaRequest.Create(anotherPayload);
                _ = await this.concentratorDeduplication.CheckDuplicateAsync(anotherRequest, this.loRaDevice);
            }

            // act
            var result = await this.concentratorDeduplication.CheckDuplicateAsync(this.loraRequest, this.loRaDevice);

            // assert
            Assert.Equal(ConcentratorDeduplication.Result.NotDuplicate, result);
            var key = ConcentratorDeduplication.CreateCacheKey(this.loraRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(this.loraRequest.StationEui, addedStation);
        }

        [Theory]
        [InlineData(true, true, ConcentratorDeduplication.Result.DuplicateDueToResubmission)]
        [InlineData(true, false, ConcentratorDeduplication.Result.DuplicateDueToResubmission)]
        [InlineData(false, true, ConcentratorDeduplication.Result.Duplicate)]
        [InlineData(false, false, ConcentratorDeduplication.Result.SoftDuplicate)]
        public async Task When_Message_Encountered_Should_Find_Duplicates_For_Different_Deduplication(bool sameStationAsBefore, bool dropDeduplicationStrategy, ConcentratorDeduplication.Result expectedResult)
        {
            // arrange
            var stationEui = this.loraRequest.StationEui;
            _ = this.concentratorDeduplication.CheckDuplicateAsync(this.loraRequest, this.loRaDevice);

            var anotherStation = sameStationAsBefore ? stationEui : new StationEui(1234);
            this.loraRequest.SetStationEui(anotherStation);

            if (!dropDeduplicationStrategy)
                this.loRaDevice.Deduplication = DeduplicationMode.Mark;

            // act/assert
            Assert.Equal(expectedResult, await this.concentratorDeduplication.CheckDuplicateAsync(this.loraRequest, this.loRaDevice));
            Assert.Equal(1, this.cache.Count);
            var key = ConcentratorDeduplication.CreateCacheKey(this.loraRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(
                (expectedResult is ConcentratorDeduplication.Result.Duplicate || expectedResult is ConcentratorDeduplication.Result.SoftDuplicate)
                    ? stationEui
                    : anotherStation,
                addedStation);
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

            // act/assert
            Assert.Throws<ArgumentException>(() => ConcentratorDeduplication.CreateCacheKey(this.loraRequest));
        }

        public void Dispose()
        {
            this.loraRequest.Dispose();
            this.loRaDevice.Dispose();
            this.connectionManager.Dispose();
            this.cache?.Dispose();
            this.concentratorDeduplication?.Dispose();
        }
    }
}
