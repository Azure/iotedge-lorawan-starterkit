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
        [InlineData("E7-EC-EB-BC-59-0B-C8-8B-37-61-FA-6C-D0-3D-74-9F-87-46-3D-AB-B6-70-21-A5-C6-76-8C-25-EC-68-B3-F2", 0, 0, 0)]
        [InlineData("E7-EC-EB-BC-59-0B-C8-8B-37-61-FA-6C-D0-3D-74-9F-87-46-3D-AB-B6-70-21-A5-C6-76-8C-25-EC-68-B3-F2", 0, 0, 0, "1")]
        [InlineData("D2-B5-F8-AB-CA-DC-0B-63-F3-6D-83-2E-9E-54-FA-BA-38-11-68-66-14-52-B2-8B-B9-7D-91-80-C4-1E-3E-A0", 0x1111111111111111UL, 0, 0)]
        [InlineData("E8-F5-83-11-F7-68-CE-49-9B-33-19-A0-49-8E-07-C9-AA-78-69-54-54-21-A5-34-85-E9-64-A2-DF-5A-26-05", 0, 1, 0)]
        [InlineData("9B-94-6D-15-EC-01-8C-3F-C5-B2-A6-02-94-4C-6C-6A-92-91-97-0B-74-CC-6F-A0-70-04-73-62-9E-EE-7B-37", 0, 0, 1)]
        public void CreateKeyMethod_Should_Return_Expected_Keys_For_Different_Data_Messages(string expectedKey, ulong devEui, ushort frameCounter, ushort mic, string? fieldNotUsedInKey = null)
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
        [InlineData("60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4", 0, 0, 0)]
        [InlineData("60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4", 0, 0, 0, (uint)1)] // a non-relevant field should not influence the key
        [InlineData("BF-67-81-DB-77-1A-EF-1C-14-55-9E-2C-22-E7-D1-CF-4F-57-77-77-65-6E-2C-D6-E3-D4-1A-6E-A1-6A-17-86", 0x101010101010101UL, 0, 0)]
        [InlineData("40-25-81-8B-EA-C7-AC-CD-32-4E-2A-02-CE-15-C8-9B-72-A4-81-32-9D-91-9F-FE-7A-62-D8-BA-3A-C4-E4-00", 0, 0x101010101010101UL, 0)]
        [InlineData("B0-EF-53-E1-22-B3-C4-0B-57-93-55-21-96-95-43-03-29-F4-6C-3B-24-93-6C-BD-73-49-67-78-0A-60-9B-E2", 0, 0, 1)]
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
