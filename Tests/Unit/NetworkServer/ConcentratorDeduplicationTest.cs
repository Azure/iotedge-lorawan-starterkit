// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using System.Linq;
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : IAsyncDisposable
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
        public async Task When_Data_Message_Not_Encountered_Should_Not_Find_Duplicates_And_Should_Add_To_Cache(bool isCacheEmpty)
        {
            // arrange
            if (!isCacheEmpty)
            {
                await using var testDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, new DevEui(0x1111111111111111UL), this.connectionManager);
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

        // Ensures that ConcentratorDeduplication.DataMessageKey can be internal, even though it is part of the return type of RawCreateKeyDataMessagesTheoryData.
        public sealed class DataMessageKeyHolder
        {
            internal DataMessageKeyHolder(ConcentratorDeduplication.DataMessageKey dataMessageKey) => Value = dataMessageKey;
            internal ConcentratorDeduplication.DataMessageKey Value { get; }
        }

        private static readonly (DevEui DevEui, Mic Mic, ushort FCnt, string? FieldNotUsedInKey)[] RawCreateKeyDataMessagesTheoryData = new (DevEui, Mic, ushort, string?)[]
        {
            (new DevEui(0), new Mic(0), 0,  null),
            (new DevEui(0), new Mic(0), 0, "1"), // a non-relevant field should not influence the key
            (new DevEui(0x1010101010101010UL), new Mic(0), 0, null),
            (new DevEui(0), new Mic(1), 0, null),
            (new DevEui(0), new Mic(0), 1, null)
        };

        public static TheoryData<DataMessageKeyHolder, DevEui, Mic, ushort, string?> CreateKeyDataMessagesTheoryData =>
            TheoryDataFactory.From(from dataPoint in RawCreateKeyDataMessagesTheoryData
                                   select (new DataMessageKeyHolder(new ConcentratorDeduplication.DataMessageKey(dataPoint.DevEui, dataPoint.Mic, dataPoint.FCnt)),
                                           dataPoint.DevEui, dataPoint.Mic, dataPoint.FCnt, dataPoint.FieldNotUsedInKey));

        [Theory]
        [MemberData(nameof(CreateKeyDataMessagesTheoryData))]
        public async Task CreateKeyMethod_Should_Return_Expected_Keys_For_Different_Data_Messages(DataMessageKeyHolder expectedKey, DevEui devEui, Mic mic, ushort frameCounter, string? fieldNotUsedInKey = null)
        {
            var options = fieldNotUsedInKey ?? string.Empty;
            await using var testDevice = new LoRaDevice(this.simulatedABPDevice.DevAddr, devEui, this.connectionManager);

            var payload = new LoRaPayloadData(this.dataPayload.DevAddr, new MacHeader(MacMessageType.ConfirmedDataUp),
                                              FrameControlFlags.None, frameCounter, options, "payload", FramePort.AppMin, mic,
                                              NullLogger.Instance);

            Assert.Equal(expectedKey.Value, ConcentratorDeduplication.CreateCacheKey(payload, testDevice));
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

        // Ensures that ConcentratorDeduplication.JoinMessageKey can be internal, even though it is part of the return type of RawCreateKeyJoinMessagesTheoryData.
        public sealed class JoinMessageKeyHolder
        {
            internal JoinMessageKeyHolder(ConcentratorDeduplication.JoinMessageKey joinMessageKey) => Value = joinMessageKey;
            internal ConcentratorDeduplication.JoinMessageKey Value { get; }
        }

        private static readonly (JoinEui JoinEui, DevEui DevEui, DevNonce DevNonce, int? FieldNotUsedInKey)[] RawCreateKeyJoinMessagesTheoryData = new (JoinEui, DevEui, DevNonce, int?)[]
        {
            (new JoinEui(0), new DevEui(0), new DevNonce(0), null),
            (new JoinEui(0), new DevEui(0), new DevNonce(0), 1),
            (new JoinEui(0x1010101010101010UL), new DevEui(0), new DevNonce(0), null),
            (new JoinEui(0), new DevEui(0x1010101010101010UL), new DevNonce(0), null),
            (new JoinEui(0), new DevEui(0), new DevNonce(1), null),
        };

        public static TheoryData<JoinMessageKeyHolder, JoinEui, DevEui, DevNonce, Mic> CreateKeyJoinMessagesTheoryData =>
            TheoryDataFactory.From(from dataPoint in RawCreateKeyJoinMessagesTheoryData
                                   select (new JoinMessageKeyHolder(new ConcentratorDeduplication.JoinMessageKey(dataPoint.JoinEui, dataPoint.DevEui, dataPoint.DevNonce)),
                                           dataPoint.JoinEui, dataPoint.DevEui, dataPoint.DevNonce, new Mic(dataPoint.FieldNotUsedInKey ?? 0)));

        [Theory]
        [MemberData(nameof(CreateKeyJoinMessagesTheoryData))]
        public void CreateCacheKey_Should_Return_Expected_Keys_For_Different_JoinRequests(JoinMessageKeyHolder expectedKey, JoinEui joinEui, DevEui devEui, DevNonce devNonce, Mic mic)
        {
            var payload = new LoRaPayloadJoinRequest(joinEui, devEui, devNonce, mic);
            Assert.Equal(expectedKey.Value, ConcentratorDeduplication.CreateCacheKey(payload));
        }
        #endregion

        public async ValueTask DisposeAsync()
        {
            await this.loRaDevice.DisposeAsync();
            this.dataRequest.Dispose();
            this.joinRequest.Dispose();

            await this.connectionManager.DisposeAsync();
            this.cache?.Dispose();
        }
    }
}
