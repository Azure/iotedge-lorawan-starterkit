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
                var anotherPayload = this.simulatedABPDevice.CreateConfirmedDataUpMessage("another_payload");
                using var anotherRequest = WaitableLoRaRequest.Create(anotherPayload);
                _ = this.concentratorDeduplication.CheckDuplicateData(anotherRequest, this.loRaDevice);
            }

            // act
            var result = this.concentratorDeduplication.CheckDuplicateData(this.dataRequest, this.loRaDevice);

            // assert
            Assert.Equal(ConcentratorDeduplicationResult.NotDuplicate, result);
            var key = this.concentratorDeduplication.CreateCacheKey(this.dataPayload);
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
            var key = this.concentratorDeduplication.CreateCacheKey(this.dataPayload);
            Assert.True(this.cache.TryGetValue(key, out var foundStation));
            Assert.Equal(station1Eui, foundStation);
        }

        [Theory, MemberData(nameof(TestPayloadData.DataGenerator), MemberType = typeof(TestPayloadData))]
        public void CreateKeyMethod_Should_Return_Expected_Keys_For_Different_Payloads(LoRaPayloadData payloadData, string expectedKey)
        {
            Assert.Equal(expectedKey, this.concentratorDeduplication.CreateCacheKey(payloadData));
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
            var key = this.concentratorDeduplication.CreateCacheKey(this.joinPayload);
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
            var key = this.concentratorDeduplication.CreateCacheKey(this.joinPayload);
            Assert.True(this.cache.TryGetValue(key, out var addedStation));
            Assert.Equal(station1Eui, addedStation);
        }

        [Fact]
        public void CreateKeyMethod_Should_Produce_Expected_Key_For_JoinRequests()
        {
            // arrange
            this.joinPayload.DevNonce = new DevNonce(0);
            this.joinRequest.SetPayload(this.joinPayload);

            var expectedKey = "60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4";

            // act/assert
            Assert.Equal(expectedKey, this.concentratorDeduplication.CreateCacheKey(this.joinPayload));
        }

        [Theory, MemberData(nameof(TestJoinRequestData.DataGenerator), MemberType = typeof(TestJoinRequestData))]
        public void CreateKeyMethod_Should_Return_Expected_Keys_For_Different_JoinRequests(LoRaPayloadJoinRequest joinRequest, string expectedKey)
        {
            Assert.Equal(expectedKey, this.concentratorDeduplication.CreateCacheKey(joinRequest));
        }
        #endregion

        private static class TestPayloadData
        {
            private static readonly Memory<byte> BaseDevAddr = new byte[] { 0, 0, 0, 0 };
            private static readonly Memory<byte> BaseMic = new byte[] { 0, 0, 0, 0 };
            private static readonly byte[] BaseRawMessage = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            private static readonly Memory<byte> BaseFcnt = new byte[] { 0, 0 };

            public static readonly TheoryData<LoRaPayloadData, string> DataGenerator = new()
            {
                { new LoRaPayloadData() { DevAddr = BaseDevAddr, Mic = BaseMic, RawMessage = BaseRawMessage, Fcnt = BaseFcnt }, "65-9D-36-CA-56-3B-A4-62-2D-AA-BB-36-A7-1D-AF-AF-60-60-CD-CB-F8-9B-B1-2E-75-42-61-98-49-6D-27-2C" },
                { new LoRaPayloadData() { DevAddr = BaseDevAddr, Mic = BaseMic, RawMessage = BaseRawMessage, Fcnt = BaseFcnt, Fopts = new byte[] { 1, 1 } }, "65-9D-36-CA-56-3B-A4-62-2D-AA-BB-36-A7-1D-AF-AF-60-60-CD-CB-F8-9B-B1-2E-75-42-61-98-49-6D-27-2C" }, // changing a field not used should not influence the key
                { new LoRaPayloadData() { DevAddr = new byte[] { 1, 1, 1, 1 }, Mic = BaseMic, RawMessage = BaseRawMessage, Fcnt = BaseFcnt }, "4A-18-25-54-88-2D-C7-33-AA-38-DA-7C-C5-EC-FE-10-BA-E2-C7-D7-78-E4-62-C0-5F-D8-23-DC-2F-30-1C-AE" }, // changing one field at the time
                { new LoRaPayloadData() { DevAddr = BaseDevAddr, Mic = new byte[] { 1, 1, 1, 1 }, RawMessage = BaseRawMessage, Fcnt = BaseFcnt }, "93-2B-A3-89-79-73-41-D2-CE-C2-76-DB-E2-B1-1F-9E-BA-DB-93-55-DF-B6-75-66-74-D5-56-4B-C8-74-78-33" },
                { new LoRaPayloadData() { DevAddr = BaseDevAddr, Mic = BaseMic, RawMessage = new byte[] { 1, 1, 1, 1 }, Fcnt = BaseFcnt }, "8D-96-DA-F6-2B-77-58-99-73-D4-06-F1-5A-27-64-D9-FF-56-2B-67-28-39-8A-66-C7-FC-49-93-21-5B-05-79" },
                { new LoRaPayloadData() { DevAddr = BaseDevAddr, Mic = BaseMic, RawMessage = Array.Empty<byte>(), Fcnt = BaseFcnt }, "01-D4-48-AF-D9-28-06-54-58-CF-67-0B-60-F5-A5-94-D7-35-AF-01-72-C8-D6-7F-22-A8-16-80-13-26-81-CA" }, // RawMessage can be empty
                { new LoRaPayloadData() { DevAddr = BaseDevAddr, Mic = BaseMic, RawMessage = null, Fcnt = BaseFcnt }, "01-D4-48-AF-D9-28-06-54-58-CF-67-0B-60-F5-A5-94-D7-35-AF-01-72-C8-D6-7F-22-A8-16-80-13-26-81-CA" },
                { new LoRaPayloadData() { DevAddr = BaseDevAddr, Mic = BaseMic, RawMessage = null, Fcnt = new byte[] { 1, 1 } }, "76-1E-D0-3D-E0-62-52-7A-A7-A8-43-5D-D6-D9-54-6D-8C-5F-0C-DD-24-97-A7-7E-75-EE-23-49-85-94-7B-3E" }
            };
        }

        private static class TestJoinRequestData
        {
            private static readonly Memory<byte> BaseAppEui = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            private static readonly Memory<byte> BaseDevEui = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            private static readonly DevNonce BaseDevNonce = new DevNonce(0);

            public static readonly TheoryData<LoRaPayloadJoinRequest, string> DataGenerator = new()
            {
                { new LoRaPayloadJoinRequest() { AppEUI = BaseAppEui, DevEUI = BaseDevEui, DevNonce = BaseDevNonce }, "60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4" },
                { new LoRaPayloadJoinRequest() { AppEUI = BaseAppEui, DevEUI = BaseDevEui, DevNonce = BaseDevNonce, Mhdr = new byte[] { 1 } }, "60-DA-A3-A5-F7-DB-FA-20-0F-8C-82-84-0E-CF-5B-42-64-0B-70-F3-B7-21-8A-4C-6B-BD-67-DB-54-2E-75-A4" },
                { new LoRaPayloadJoinRequest() { AppEUI = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }, DevEUI = BaseDevEui, DevNonce = BaseDevNonce }, "BF-67-81-DB-77-1A-EF-1C-14-55-9E-2C-22-E7-D1-CF-4F-57-77-77-65-6E-2C-D6-E3-D4-1A-6E-A1-6A-17-86" },
                { new LoRaPayloadJoinRequest() { AppEUI = BaseAppEui, DevEUI = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }, DevNonce = BaseDevNonce }, "40-25-81-8B-EA-C7-AC-CD-32-4E-2A-02-CE-15-C8-9B-72-A4-81-32-9D-91-9F-FE-7A-62-D8-BA-3A-C4-E4-00" },
                { new LoRaPayloadJoinRequest() { AppEUI = BaseAppEui, DevEUI = BaseDevEui, DevNonce = new DevNonce(1) }, "B0-EF-53-E1-22-B3-C4-0B-57-93-55-21-96-95-43-03-29-F4-6C-3B-24-93-6C-BD-73-49-67-78-0A-60-9B-E2" },
            };
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
