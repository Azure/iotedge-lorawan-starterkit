// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using System.Security.Cryptography;
    using System.Text;
    using BasicsStation;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    internal sealed class ConcentratorDeduplication<T> : IConcentratorDeduplication<T>, IDisposable
    {
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache cache;
        private readonly WebSocketWriterRegistry<StationEui, string> socketRegistry;
        private readonly ILogger<IConcentratorDeduplication<T>> logger;

        [ThreadStatic]
        private static SHA256? sha256;

        private static SHA256 Sha256 => sha256 ??= SHA256.Create();

        public ConcentratorDeduplication(
            IMemoryCache cache,
            WebSocketWriterRegistry<StationEui, string> socketRegistry,
            ILogger<IConcentratorDeduplication<T>> logger)
        {
            this.cache = cache;
            this.socketRegistry = socketRegistry;
            this.logger = logger;
        }

        public bool ShouldDrop(T frame, StationEui stationEui)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            var key = CreateCacheKey(frame);

            StationEui previousStation;
            lock (this.cache)
            {
                if (!this.cache.TryGetValue(key, out previousStation))
                {
                    AddToCache(key, stationEui);
                    return false;
                }
            }

            if (previousStation == stationEui)
            {
                // considered as a resubmit
                this.logger.LogDebug($"Message received from the same EUI {stationEui} as before, will not drop.");
                return false;
            }

            // received from a different station
            if (this.socketRegistry.IsSocketWriterOpen(previousStation))
            {
                this.logger.LogInformation($"Duplicate message received from station with EUI {stationEui}, will drop.");
                return true;
            }

            this.logger.LogInformation($"Connectivity to previous station with EUI {previousStation}, was lost, will not drop and will use station with EUI {stationEui} from now onwards.");
            AddToCache(key, stationEui);
            return false;
        }

        internal static string CreateCacheKey(T message)
        {
            _ = message ?? throw new ArgumentNullException(nameof(message));

            if (message.GetType() == typeof(UpstreamDataFrame))
            {
                var updf = message as UpstreamDataFrame;
                _ = updf ?? throw new ArgumentException($"Data frame {updf} not well formed");
                return CreateCacheKeyCore(updf);
            }

            if (message.GetType() == typeof(JoinRequestFrame))

            {
                var joinReq = message as JoinRequestFrame;
                _ = joinReq ?? throw new ArgumentException($"Join request {joinReq} not well formed");
                return CreateCacheKeyCore(joinReq);
            }

            throw new ArgumentException($"{message} not of proper type");
        }

        private static string CreateCacheKeyCore(UpstreamDataFrame updf)
        {
            var totalBufferLength = DevAddr.Size + Mic.Size + updf.Payload.Length + sizeof(ushort);
            var buffer = totalBufferLength <= 128 ? stackalloc byte[totalBufferLength] : new byte[totalBufferLength]; // uses the stack for small allocations, otherwise the heap
            var head = buffer; // keeps a view pointing at the start of the buffer
            buffer = updf.DevAddr.Write(buffer);
            buffer = updf.Mic.Write(buffer);
            _ = Encoding.UTF8.GetBytes(updf.Payload, buffer);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[updf.Payload.Length..], updf.Counter);

            var key = Sha256.ComputeHash(head.ToArray());

            return BitConverter.ToString(key);
        }

        private static string CreateCacheKeyCore(JoinRequestFrame joinReq)
        {
            var totalBufferLength = JoinEui.Size + DevEui.Size + DevNonce.Size;
            Span<byte> buffer = stackalloc byte[totalBufferLength];
            var head = buffer; // keeps a view pointing at the start of the buffer
            buffer = joinReq.JoinEui.Write(buffer);
            buffer = joinReq.DevEui.Write(buffer);
            buffer = joinReq.DevNonce.Write(buffer);

            var key = Sha256.ComputeHash(head.ToArray());

            return BitConverter.ToString(key);
        }

        private void AddToCache(string key, StationEui stationEui)
            => this.cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = DefaultExpiration
            });

        public void Dispose()
        {
            this.cache.Dispose();
        }
    }
}
