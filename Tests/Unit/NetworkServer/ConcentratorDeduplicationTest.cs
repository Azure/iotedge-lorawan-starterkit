// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

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

        public static TheoryData<byte[]?> TestRawMessages =>
            new TheoryData<byte[]?>
            {
                null,
                Array.Empty<byte>(),
                new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }
            };

        public ConcentratorDeduplicationTest()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);

            this.simulatedABPDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            var dataPayload = this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload");
            this.dataRequest = WaitableLoRaRequest.Create(dataPayload);
            this.loRaDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, this.simulatedABPDevice.DevEUI, this.connectionManager);

            this.simulatedOTAADevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            var joinPayload = this.simulatedOTAADevice.CreateJoinRequest(appkey: this.simulatedOTAADevice.AppKey);
            this.joinRequest = WaitableLoRaRequest.Create(joinPayload);
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
            var key = this.concentratorDeduplication.CreateCacheKey((LoRaPayloadData)this.dataRequest.Payload);
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
            var key = this.concentratorDeduplication.CreateCacheKey((LoRaPayloadData)this.dataRequest.Payload);
            Assert.True(this.cache.TryGetValue(key, out var foundStation));
            Assert.Equal(station1Eui, foundStation);
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key_For_UpstreamDataFrames()
        {
            // arrange
            var expectedKey = "EF-BC-21-12-E9-B8-AF-F4-1B-F5-7B-7D-3C-D3-69-5A-84-E3-D8-6C-FC-1A-8E-BC-80-39-10-64-0D-6B-B3-7C";

            // act/assert
            Assert.Equal(expectedKey, this.concentratorDeduplication.CreateCacheKey((LoRaPayloadData)this.dataRequest.Payload));
        }

        [Theory, MemberData(nameof(TestRawMessages))]
        public void CreateKeyMethod_Should_Include_All_Properties_For_Different_RawMessages(byte[]? rawMessage)
        {
            // arrange
            var mock = new Mock<ConcentratorDeduplication>(MockBehavior.Default, this.cache, NullLogger<ConcentratorDeduplication>.Instance);
            Memory<byte> actualBuffer = null;
            _ = mock.Setup(x => x.HashKey(It.IsAny<byte[]>())).Callback<byte[]>(b => actualBuffer = b);
            var payload = (LoRaPayloadData)this.dataRequest.Payload;
            payload.RawMessage = rawMessage;

            // act
            _ = mock.Object.CreateCacheKey(payload);

            // assert
            mock.Verify(x => x.HashKey(It.IsAny<byte[]>()), Times.Once);
            Assert.True(MemoryExtensions.SequenceEqual(payload.DevAddr.Span, actualBuffer.Span[0..4]));
            Assert.True(MemoryExtensions.SequenceEqual(payload.Mic.Span, actualBuffer.Span[4..8]));
            var index = 8;
            if (payload.RawMessage?.Length > 0)
            {
                index += payload.RawMessage.Length;
                Assert.True(MemoryExtensions.SequenceEqual(payload.RawMessage.AsSpan(), actualBuffer.Span[8..index]));
            }
            Assert.True(MemoryExtensions.SequenceEqual(payload.Fcnt.Span, actualBuffer.Span[index..])); // implicitly asserts that the length is correct as well
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
                using var anotherJoinRequest = WaitableLoRaRequest.Create(anotherJoinPayload);
                anotherJoinRequest.SetPayload(anotherJoinPayload);

                _ = this.concentratorDeduplication.CheckDuplicateJoin(anotherJoinRequest);
            }

            // act
            var result = this.concentratorDeduplication.CheckDuplicateJoin(this.joinRequest);

            // assert
            Assert.Equal(ConcentratorDeduplicationResult.NotDuplicate, result);
            var key = this.concentratorDeduplication.CreateCacheKey((LoRaPayloadJoinRequest)this.joinRequest.Payload);
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
            var key = this.concentratorDeduplication.CreateCacheKey((LoRaPayloadJoinRequest)this.joinRequest.Payload);
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
            Assert.Equal(expectedKey, this.concentratorDeduplication.CreateCacheKey((LoRaPayloadJoinRequest)this.joinRequest.Payload));
        }
        #endregion

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
