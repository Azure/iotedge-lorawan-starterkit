// Copyright (c) Microsoft. All rights reserved.
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
        private static readonly TimeSpan CacheEntryExpiration = TimeSpan.FromMinutes(1);

        internal readonly Dictionary<StationEui, IWebSocketWriter<string>> webSocketRegistry;
        private readonly ILogger logger;

        public ConcentratorDeduplication(ILogger<ConcentratorDeduplication> logger) // TODO depends on WebSocketRegistry, will be added with #676
        {
            Cache = new MemoryCache(new MemoryCacheOptions());
            this.webSocketRegistry = new Dictionary<StationEui, IWebSocketWriter<string>>();
            this.logger = logger;
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
                        if (IsConnectionOpen(stationEui))
                        {
                            return false; // it's a resubmit
                        }

                        this.logger.Log(LogLevel.Debug, $"No open web socket connection found for {stationEui}, dropping message.");

                        // TODO shall we remove from cache as well?
                        return true;
                    }
                    else
                    {
                        if (IsConnectionOpen(previousMessageFromDevice))
                        {
                            // we still have a connection open to the station used last time
                            return true;
                        }
                        else if (IsConnectionOpen(stationEui))
                        {
                            AddToCache(key, stationEui);
                            return false;
                        }
                        else
                        {
                            this.logger.Log(LogLevel.Debug, $"No open web socket connection found for {stationEui} or {previousMessageFromDevice}, dropping message.");

                            // TODO shall we remove from cache as well?
                            return true;
                        }
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

        internal bool IsConnectionOpen(StationEui stationEui)
        {
            _ = this.webSocketRegistry.TryGetValue(stationEui, out var socket);

            return socket != null && !socket.IsClosed;
        }

        private void AddToCache(string key, StationEui stationEui)
            => Cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = CacheEntryExpiration
            });

        public void Dispose() => Cache.Dispose();
    }
}
