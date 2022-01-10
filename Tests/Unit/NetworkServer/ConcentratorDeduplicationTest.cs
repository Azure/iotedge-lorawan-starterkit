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
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly MemoryCache cache; // ownership passed to ConcentratorDeduplication
        private readonly LoRaDeviceClientConnectionManager connectionManager;
        private readonly ConcentratorDeduplication concentratorDeduplication;

        private readonly LoRaDevice loRaDevice;
        private readonly SimulatedDevice simulatedABPDevice;
        private readonly LoRaPayloadData dataPayload;
        private readonly SimulatedDevice simulatedOTAADevice;
        private readonly LoRaPayloadJoinRequest joinPayload;
        private readonly WaitableLoRaRequest dataRequest;
        private readonly WaitableLoRaRequest joinRequest;

        public ConcentratorDeduplicationTest()
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);

            this.simulatedABPDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            this.dataPayload = this.simulatedABPDevice.CreateConfirmedDataUpMessage("payload");
            this.dataRequest = WaitableLoRaRequest.Create(this.dataPayload);
            this.loRaDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, this.simulatedABPDevice.DevEUI, this.connectionManager);

            this.simulatedOTAADevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(0));
            this.joinPayload = this.simulatedOTAADevice.CreateJoinRequest(appkey: this.simulatedOTAADevice.AppKey);
            this.joinRequest = WaitableLoRaRequest.Create(this.joinPayload);
            this.joinRequest.SetPayload(this.joinPayload);

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
                using var testDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, new DevEui(0x1111111111111111UL).ToString(), this.connectionManager);
                _ = this.concentratorDeduplication.CheckDuplicateData(this.dataRequest, testDevice);
            }

            // act
            var result = this.concentratorDeduplication.CheckDuplicateData(this.dataRequest, this.loRaDevice);

            // assert
            Assert.Equal(ConcentratorDeduplicationResult.NotDuplicate, result);
            var key = ConcentratorDeduplication.CreateCacheKey(this.dataPayload, this.loRaDevice);
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
        public void When_Data_Message_Encountered_Should_Find_Duplicates_For_Different_Deduplication_Strategies(string station1, string station2, DeduplicationMode deduplicationMode, ConcentratorDeduplicationResult expectedResult)
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
            var key = ConcentratorDeduplication.CreateCacheKey(this.dataPayload, this.loRaDevice);
            Assert.True(this.cache.TryGetValue(key, out var foundStation));
            Assert.Equal(station1Eui, foundStation);
        }

        [Theory]
        [InlineData("00-00-00-00-00-00-00-00-00-00-00-00-00-00", 0, 0, 0)]
        [InlineData("00-00-00-00-00-00-00-00-00-00-00-00-00-00", 0, 0, 0, "1")] // a non-relevant field should not influence the key
        [InlineData("10-10-10-10-10-10-10-10-00-00-00-00-00-00", 0x1010101010101010UL, 0, 0)]
        [InlineData("00-00-00-00-00-00-00-00-01-00-00-00-00-00", 0, 1, 0)]
        [InlineData("00-00-00-00-00-00-00-00-00-00-00-00-01-00", 0, 0, 1)]
        public void CreateKeyMethod_Should_Return_Expected_Keys_For_Different_Data_Messages(string expectedKey, ulong devEui, ushort mic, ushort frameCounter, string? fieldNotUsedInKey = null)
        {
            var options = fieldNotUsedInKey ?? string.Empty;
            using var testDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, new DevEui(devEui).ToString(), this.connectionManager);

            var payload = new LoRaPayloadDataLns(this.dataPayload.DevAddr, new MacHeader(MacMessageType.ConfirmedDataUp),
                                                 frameCounter, options, "payload", new Mic(mic));

            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(payload, testDevice));
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
            var key = ConcentratorDeduplication.CreateCacheKey(this.joinPayload);
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
            var key = ConcentratorDeduplication.CreateCacheKey(this.joinPayload);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(station1Eui, addedStation);
        }

        [Theory]
        [InlineData("00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00", 0, 0, 0)]
        [InlineData("00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00", 0, 0, 0, (uint)1)] // a non-relevant field should not influence the key
        [InlineData("10-10-10-10-10-10-10-10-00-00-00-00-00-00-00-00-00-00", 0x1010101010101010UL, 0, 0)]
        [InlineData("00-00-00-00-00-00-00-00-10-10-10-10-10-10-10-10-00-00", 0, 0x1010101010101010UL, 0)]
        [InlineData("00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-01-00", 0, 0, 1)]
        public void CreateCacheKey_Should_Return_Expected_Keys_For_Different_JoinRequests(string expectedKey, ulong joinEui, ulong devEui, ushort devNonce, uint? fieldNotUsedInKey = null)
        {
            var micValue = fieldNotUsedInKey ?? 0;
            var payload = new LoRaPayloadJoinRequestLns(new MacHeader(MacMessageType.JoinRequest),
                                                        new JoinEui(joinEui), new DevEui(devEui),
                                                        new DevNonce(devNonce), new Mic(micValue));

            Assert.Equal(expectedKey, ConcentratorDeduplication.CreateCacheKey(payload));
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
