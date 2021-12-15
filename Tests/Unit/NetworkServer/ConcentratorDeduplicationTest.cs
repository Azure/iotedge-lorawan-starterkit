// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly MemoryCache cache; // ownership passed to ConcentratorDeduplication
        private readonly LoRaDeviceClientConnectionManager connectionManager;
        private readonly ConcentratorDeduplication concentratorDeduplication;

        private readonly LoRaDevice loRaDevice;
        private readonly SimulatedDevice simulatedABPDevice;
        private readonly SimulatedDevice simulatedOTAADevice;
        private readonly WaitableLoRaRequest dataRequest;
        private readonly WaitableLoRaRequest joinRequest;

        public ConcentratorDeduplicationTest()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);

            this.simulatedABPDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload");
            this.dataRequest = WaitableLoRaRequest.Create(dataPayload);
            this.loRaDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, this.simulatedABPDevice.DevEUI, this.connectionManager);

            this.simulatedOTAADevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            var joinPayload = this.simulatedOTAADevice.CreateJoinRequest();
            this.joinRequest = WaitableLoRaRequest.Create(joinPayload.SerializeUplink(this.simulatedOTAADevice.AppKey).Rxpk[0]);
            this.joinRequest.SetPayload(joinPayload);

            this.concentratorDeduplication = new ConcentratorDeduplication(
                this.cache,
                NullLogger<IConcentratorDeduplication>.Instance);
        }

        #region DataMessages
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Data_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Should_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            if (!isCacheEmpty)
            {
                var anotherPayload = this.simulatedABPDevice.CreateConfirmedDataUpMessage("another_payload");
                using var anotherRequest = WaitableLoRaRequest.Create(anotherPayload);
                _ = this.concentratorDeduplication.CheckDuplicateData(anotherRequest, this.loRaDevice);
            }

            // act
            var result = this.concentratorDeduplication.CheckDuplicateData(this.dataRequest, this.loRaDevice);

            // assert
            Assert.Equal(ConcentratorDeduplicationResult.NotDuplicate, result);
            var key = ConcentratorDeduplication.CreateCacheKey(this.dataRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(this.dataRequest.StationEui, addedStation);
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Drop, ConcentratorDeduplicationResult.DuplicateDueToResubmission)]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.Mark, ConcentratorDeduplicationResult.DuplicateDueToResubmission)]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", DeduplicationMode.None, ConcentratorDeduplicationResult.DuplicateDueToResubmission)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Drop, ConcentratorDeduplicationResult.Duplicate)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.Mark, ConcentratorDeduplicationResult.SoftDuplicateDueToDeduplicationStrategy)]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", DeduplicationMode.None, ConcentratorDeduplicationResult.SoftDuplicateDueToDeduplicationStrategy)]
        public void When_Data_Message_Encountered_Should_Find_Duplicates_For_Different_Deduplication(string station1, string station2, DeduplicationMode deduplicationMode, ConcentratorDeduplicationResult expectedResult)
        {
            // arrange
            var station1Eui = StationEui.Parse(station1);
            this.dataRequest.SetStationEui(station1Eui);
            _ = this.concentratorDeduplication.CheckDuplicateData(this.dataRequest, this.loRaDevice);

            this.dataRequest.SetStationEui(StationEui.Parse(station2));
            this.loRaDevice.Deduplication = deduplicationMode;

            // act/assert
            Assert.Equal(expectedResult, this.concentratorDeduplication.CheckDuplicateData(this.dataRequest, this.loRaDevice));
            Assert.Equal(1, this.cache.Count);
            var key = ConcentratorDeduplication.CreateCacheKey(this.dataRequest);
            Assert.True(this.cache.TryGetValue(key, out var foundStation));
            Assert.Equal(station1Eui, foundStation);
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key_For_UpstreamDataFrames()
        {
            // arrange
            var expectedKey = "13-DD-E3-E3-41-1B-29-DF-1C-3F-A8-EF-6D-BB-51-99-84-BD-90-03-B4-FA-5E-3A-ED-C2-0F-3A-5B-6B-80-C5";

            // act/assert
            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(this.dataRequest));
        }
        #endregion

        #region JoinRequests
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Join_Request_Not_Encountered_Should_Not_Find_Duplicates_And_Should_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            if (!isCacheEmpty)
            {
                var anotherJoinPayload = this.simulatedOTAADevice.CreateJoinRequest();
                using var anotherJoinRequest = WaitableLoRaRequest.Create(anotherJoinPayload.SerializeUplink(this.simulatedOTAADevice.AppKey).Rxpk[0]);
                anotherJoinRequest.SetPayload(anotherJoinPayload);

                _ = this.concentratorDeduplication.CheckDuplicateJoin(anotherJoinRequest);
            }

            // act
            var result = this.concentratorDeduplication.CheckDuplicateJoin(this.joinRequest);

            // assert
            Assert.Equal(ConcentratorDeduplicationResult.NotDuplicate, result);
            var key = ConcentratorDeduplication.CreateCacheKey(this.joinRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(this.joinRequest.StationEui, addedStation);
        }

        [Theory]
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11")]
        [InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22")]
        public void When_Join_Request_Encountered_Should_Find_Duplicate(string station1, string station2)
        {
            // arrange
            var station1Eui = StationEui.Parse(station1);
            this.joinRequest.SetStationEui(station1Eui);
            _ = this.concentratorDeduplication.CheckDuplicateJoin(this.joinRequest);

            this.joinRequest.SetStationEui(StationEui.Parse(station2));

            // act
            var result = this.concentratorDeduplication.CheckDuplicateJoin(this.joinRequest);

            // assert
            Assert.Equal(ConcentratorDeduplicationResult.Duplicate, result);
            var key = ConcentratorDeduplication.CreateCacheKey(this.joinRequest);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(station1Eui, addedStation);
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key_For_JoinRequests()
        {
            // arrange
            var joinPayload = (LoRaPayloadJoinRequest)this.joinRequest.Payload;
            joinPayload.DevNonce = new DevNonce(0);
            this.joinRequest.SetPayload(joinPayload);

            var expectedKey = "60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4";

            // act/assert
            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(this.joinRequest));
        }
        #endregion

        [Fact]
        public void CreateKeyMethod_Should_Throw_When_Used_With_Wrong_Type()
        {
            // arrange
            this.dataRequest.SetPayload(new Mock<LoRaPayloadJoinAccept>().Object);

            // act/assert
            Assert.Throws<ArgumentException>(() => ConcentratorDeduplication.CreateCacheKey(this.dataRequest));
        }

        public void Dispose()
        {
            this.loRaDevice.Dispose();
            this.dataRequest.Dispose();
            this.joinRequest.Dispose();

            this.connectionManager.Dispose();
            this.cache?.Dispose();
        }
    }
}
