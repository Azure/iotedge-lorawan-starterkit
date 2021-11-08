// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication, IDisposable
    {
        internal readonly MemoryCache Cache;

        private readonly ILogger<IConcentratorDeduplication> logger;
        private readonly TimeSpan cacheEntryExpiration;

        public ConcentratorDeduplication(
            ILogger<IConcentratorDeduplication> logger,
            int cacheEntryExpirationInMilliSeconds = 60_000)
        {
            this.Cache = new MemoryCache(new MemoryCacheOptions());
            this.logger = logger;
            this.cacheEntryExpiration = TimeSpan.FromMilliseconds(cacheEntryExpirationInMilliSeconds);
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
                this.logger.LogDebug($"Message received from the same DevAddr: {updf.DevAddr} as before, considered a resubmit.");
                return false;
            }

            this.logger.LogInformation($"Duplicate message detected from DevAddr: {updf.DevAddr}, dropping.");
            return true;
        }

        internal static string CreateCacheKey(UpstreamDataFrame updf)
        {
            using var sha256 = SHA256.Create();

            var builder = new StringBuilder();
            _ = builder.Append(updf.DevAddr).Append(updf.Mic).Append(updf.Counter).Append(updf.Payload);
            var key = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return BitConverter.ToString(key);
        }

        private void AddToCache(string key, StationEui stationEui)
            => this.Cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = this.cacheEntryExpiration
            });

        public void Dispose() => this.Cache.Dispose();
    }
}
