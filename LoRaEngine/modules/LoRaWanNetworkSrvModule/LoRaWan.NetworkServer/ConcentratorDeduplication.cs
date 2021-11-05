// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Caching.Memory;
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication, IDisposable
    {
        internal MemoryCache Cache { get; private set; }
        private static readonly TimeSpan CacheEntryExpiration = TimeSpan.FromMinutes(1);

        public ConcentratorDeduplication()
        {
            Cache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Detects duplicate data frames. 
        /// </summary>
        /// <param name="updf"></param>
        /// <param name="stationEui"></param>
        /// <returns>True if dataframe has been encountered in the past and should be dropped.</returns>
        public bool IsDuplicate(UpstreamDataFrame updf, StationEui stationEui)
        {
            if (updf == null) throw new ArgumentNullException(nameof(updf));

            var key = CreateCacheKey(updf);

            lock (Cache)
            {
                if (!Cache.TryGetValue(key, out StationEui previousMessageFromDevice))
                {
                    // this message has not been encountered before
                    AddToCache(key, stationEui);
                    return false;
                }
                else
                {
                    if (previousMessageFromDevice == stationEui)
                    {
                        // received from the same station as before
                        return false; // it's a resubmit
                    }
                    else
                    {
                        return true;
                    }
                }
            }
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
                AbsoluteExpirationRelativeToNow = CacheEntryExpiration
            });

        public void Dispose() => Cache.Dispose();
    }
}
