// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using System.Security.Cryptography;
    using System.Text;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication, IDisposable
    {
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache cache;
        private readonly ILogger<IConcentratorDeduplication> logger;

        [ThreadStatic]
        private static SHA256 sha256;

        private static SHA256 Sha256 => sha256 ??= SHA256.Create();

        public ConcentratorDeduplication(
            IMemoryCache cache,
            ILogger<IConcentratorDeduplication> logger)
        {
            this.cache = cache;
            this.logger = logger;
        }

        public bool ShouldDrop(UpstreamDataFrame updf, StationEui stationEui)
        {
            if (updf == null) throw new ArgumentNullException(nameof(updf));

            var key = CreateCacheKey(updf);

            if (!this.cache.TryGetValue(key, out StationEui previousStation))
            {
                AddToCache(key, stationEui);
                return false;
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
            var totalBufferLength = DevAddr.Size + Mic.Size + updf.Payload.Length + sizeof(ushort);
            var buffer = totalBufferLength <= 128 ? stackalloc byte[totalBufferLength] : new byte[totalBufferLength];
            var head = buffer;
            buffer = updf.DevAddr.Write(buffer);
            buffer = updf.Mic.Write(buffer);
            _ = Encoding.UTF8.GetBytes(updf.Payload, buffer);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[updf.Payload.Length..], updf.Counter);

            var key = Sha256.ComputeHash(head.ToArray());

            return BitConverter.ToString(key);
        }

        private void AddToCache(string key, StationEui stationEui)
            => this.cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = DefaultExpiration
            });

        public void Dispose() => this.cache.Dispose();
    }
}
