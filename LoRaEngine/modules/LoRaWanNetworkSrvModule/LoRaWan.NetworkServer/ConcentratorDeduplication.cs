﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication, IDisposable
    {
        internal MemoryCache Cache { get; private set; }
        internal readonly WebSocketWriterRegistry<StationEui, string> SocketRegistry;

        private readonly ILogger<IConcentratorDeduplication> Logger;
        private readonly TimeSpan CacheEntryExpiration;

        public ConcentratorDeduplication(
            WebSocketWriterRegistry<StationEui, string> socketRegistry,
            ILogger<IConcentratorDeduplication> logger,
            int cacheEntryExpirationInMilliSeconds = 60_000)
        {
            Cache = new MemoryCache(new MemoryCacheOptions());
            this.SocketRegistry = socketRegistry;
            this.Logger = logger;
            this.CacheEntryExpiration = TimeSpan.FromMilliseconds(cacheEntryExpirationInMilliSeconds);
        }

        public bool ShouldDrop(UpstreamDataFrame updf, StationEui stationEui)
        {
            if (updf == null) throw new ArgumentNullException(nameof(updf));

            var key = CreateCacheKey(updf);
            StationEui previousStation;

            lock (Cache)
            {
                if (!Cache.TryGetValue(key, out previousStation))
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

            this.Logger.LogInformation($"Duplicate message detected from DevAddr: {updf.DevAddr}, dropping.");
            return true;
        }

        internal static string CreateCacheKey(UpstreamDataFrame updf)
        {
            using var sha256 = SHA256.Create();
            var key = sha256.ComputeHash(Encoding.UTF8.GetBytes(string.Join("", updf.DevAddr, updf.FrameCounter, updf.FRMPayload, updf.Mic)));

            return BitConverter.ToString(key);
        }

        private void AddToCache(string key, StationEui stationEui)
            => Cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = this.CacheEntryExpiration
            });

        public void Dispose() => Cache.Dispose();
    }
}
