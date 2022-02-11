// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication
    {
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache cache;
        private readonly ILogger<IConcentratorDeduplication> logger;
        private static readonly object CacheLock = new object();

        internal sealed record DataMessageKey(DevEui DevEui, Mic Mic, ushort FCnt);

        internal sealed record JoinMessageKey(JoinEui JoinEui, DevEui DevEui, DevNonce DevNonce);

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

        private bool EnsureFirstMessageInCache(object key, LoRaRequest loRaRequest, out StationEui previousStation)
        {
            var stationEui = loRaRequest.StationEui;

            lock (CacheLock)
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

        internal static DataMessageKey CreateCacheKey(LoRaPayloadData payload, LoRaDevice loRaDevice) =>
            payload.Mic is { } someMic
                ? new DataMessageKey(loRaDevice.DevEUI, someMic, payload.Fcnt)
                : throw new ArgumentException(nameof(payload.Mic));

        internal static JoinMessageKey CreateCacheKey(LoRaPayloadJoinRequest payload) =>
            new JoinMessageKey(payload.AppEui, payload.DevEUI, payload.DevNonce);
    }
}
