// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication
    {
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);
        internal const string DataMessageCacheKeyPrefix = "datamessage:";
        internal const string JoinMessageCacheKeyPrefix = "joinmessage:";

        private readonly IMemoryCache cache;
        private readonly ILogger<IConcentratorDeduplication> logger;
        private static readonly object cacheLock = new object();

        public ConcentratorDeduplication(
            IMemoryCache cache,
            ILogger<IConcentratorDeduplication> logger)
        {
            this.cache = cache;
            this.logger = logger;
        }

        public ConcentratorDeduplicationResult CheckDuplicateJoin(LoRaRequest loRaRequest)
        {
            _ = loRaRequest ?? throw new ArgumentNullException(nameof(loRaRequest));
            var key = CreateCacheKey((LoRaPayloadJoinRequest)loRaRequest.Payload);
            if (EnsureFirstMessageInCache(key, loRaRequest, out _))
                return ConcentratorDeduplicationResult.NotDuplicate;

            var result = ConcentratorDeduplicationResult.Duplicate;

            this.logger.LogDebug($"Join received from station {loRaRequest.StationEui}. Marked as {result} {Constants.MessageAlreadyEncountered}.");
            return result;
        }

        public ConcentratorDeduplicationResult CheckDuplicateData(LoRaRequest loRaRequest, LoRaDevice loRaDevice)
        {
            _ = loRaRequest ?? throw new ArgumentNullException(nameof(loRaRequest));
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));

            var key = CreateCacheKey((LoRaPayloadData)loRaRequest.Payload, loRaDevice);
            if (EnsureFirstMessageInCache(key, loRaRequest, out var previousStation))
                return ConcentratorDeduplicationResult.NotDuplicate;

            var result = ConcentratorDeduplicationResult.SoftDuplicateDueToDeduplicationStrategy;

            if (previousStation == loRaRequest.StationEui)
            {
                result = ConcentratorDeduplicationResult.DuplicateDueToResubmission;
            }
            else if (loRaDevice.Deduplication == DeduplicationMode.Drop)
            {
                result = ConcentratorDeduplicationResult.Duplicate;
            }

            this.logger.LogDebug($"Data message received from station {loRaRequest.StationEui}. Marked as {result} {Constants.MessageAlreadyEncountered}.");
            return result;
        }

        private bool EnsureFirstMessageInCache(string key, LoRaRequest loRaRequest, out StationEui previousStation)
        {
            var stationEui = loRaRequest.StationEui;

            lock (cacheLock)
            {
                if (!this.cache.TryGetValue(key, out previousStation))
                {
                    _ = this.cache.Set(key, stationEui, new MemoryCacheEntryOptions()
                    {
                        SlidingExpiration = DefaultExpiration
                    });
                    return true;
                }
            }

            return false;
        }

        internal static string CreateCacheKey(LoRaPayloadData payload, LoRaDevice loRaDevice)
        {
            var totalBufferLength = DevEui.Size + Mic.Size + payload.Fcnt.Length;
            Span<byte> buffer = stackalloc byte[totalBufferLength];
            var head = buffer; // keeps a view pointing at the start of the buffer

            buffer = DevEui.Parse(loRaDevice.DevEUI.AsSpan()).Write(buffer);
            buffer = payload.Mic is { } someMic ? someMic.Write(buffer) : throw new InvalidOperationException("Mic must not be null.");
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, BinaryPrimitives.ReadUInt16LittleEndian(payload.Fcnt.Span));

            return CreateCacheKeyCore(DataMessageCacheKeyPrefix, head);
        }

        internal static string CreateCacheKey(LoRaPayloadJoinRequest payload)
        {
            var joinEui = JoinEui.Read(payload.AppEUI.Span);
            var devEui = DevEui.Read(payload.DevEUI.Span);

            var totalBufferLength = JoinEui.Size + DevEui.Size + DevNonce.Size;
            Span<byte> buffer = stackalloc byte[totalBufferLength];
            var head = buffer; // keeps a view pointing at the start of the buffer

            buffer = joinEui.Write(buffer);
            buffer = devEui.Write(buffer);

            var devNonce = payload.DevNonce;
            _ = devNonce.Write(buffer);

            return CreateCacheKeyCore(JoinMessageCacheKeyPrefix, head);
        }

        private static string CreateCacheKeyCore(string prefix, ReadOnlySpan<byte> buffer)
        {
            Span<char> key = stackalloc char[(buffer.Length * 3) - 1];
            Hexadecimal.Write(buffer, key, separator: '-');
            return string.Concat(prefix, key.ToString());
        }
    }
}
