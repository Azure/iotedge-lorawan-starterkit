// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Security.Cryptography;
    using System.Text;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication, IDisposable
    {
        internal readonly MemoryCache Cache;
        internal readonly WebSocketWriterRegistry<StationEui, string> SocketRegistry;

        private readonly ILogger<IConcentratorDeduplication> Logger;
        private readonly TimeSpan CacheEntryExpiration;

        public ConcentratorDeduplication(
            WebSocketWriterRegistry<StationEui, string> socketRegistry,
            ILogger<IConcentratorDeduplication> logger,
            int cacheEntryExpirationInMilliSeconds = 60_000)
        {
            this.Cache = new MemoryCache(new MemoryCacheOptions());
            this.SocketRegistry = socketRegistry;
            this.Logger = logger;
            this.CacheEntryExpiration = TimeSpan.FromMilliseconds(cacheEntryExpirationInMilliSeconds);
        }

        public bool ShouldDrop(UpstreamDataFrame updf, StationEui stationEui)
        {
            if (updf == null) throw new ArgumentNullException(nameof(updf));

            var key = CreateCacheKey(updf);
            StationEui previousStation;

            lock (this.Cache)
            {
                if (!this.Cache.TryGetValue(key, out previousStation))
                {
                    AddToCache(key, stationEui);
                    return false;
                }
            }

            if (previousStation == stationEui)
            {
                this.Logger.LogDebug($"Message received from the same DevAddr: {updf.DevAddr} as before, considered a resubmit.");
                return false;
            }

            // received from a different station
            if (IsConnectionOpen(previousStation))
            {
                this.Logger.LogInformation($"Duplicate message received from station with EUI: {stationEui}, dropping.");
                return true;
            }

            this.Logger.LogInformation($"Connectivity to previous station with EUI {previousStation}, was lost, will use {stationEui} from now onwards.");
            lock (this.Cache)
                AddToCache(key, stationEui);

            return false;
        }

        internal static string CreateCacheKey(UpstreamDataFrame updf)
        {
            using var sha256 = SHA256.Create();
            var key = sha256.ComputeHash(Encoding.UTF8.GetBytes(string.Join("", updf.DevAddr, updf.FrameCounter, updf.FRMPayload, updf.Mic)));

            return BitConverter.ToString(key);
        }

        private bool IsConnectionOpen(StationEui stationEui)
            => this.SocketRegistry.IsSocketWriterOpen(stationEui);

        private void AddToCache(string key, StationEui stationEui)
            => this.Cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = this.CacheEntryExpiration
            });

        public void Dispose() => this.Cache.Dispose();
    }
}
