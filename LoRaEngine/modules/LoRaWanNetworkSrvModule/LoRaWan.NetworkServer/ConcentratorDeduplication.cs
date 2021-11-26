// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using System.Security.Cryptography;
    using DotNetty.Buffers;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    internal sealed class ConcentratorDeduplication :
        IConcentratorDeduplication, IDisposable
    {
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache cache;
        private readonly IDeduplicationStrategyFactory deduplicationStrategy;
        private readonly WebSocketWriterRegistry<StationEui, string> socketRegistry;
        private readonly ILogger<IConcentratorDeduplication> logger;

        [ThreadStatic]
        private static SHA256? sha256;

        private static SHA256 Sha256 => sha256 ??= SHA256.Create();

        public ConcentratorDeduplication(
            IMemoryCache cache,
            IDeduplicationStrategyFactory deduplicationStrategy,
            WebSocketWriterRegistry<StationEui, string> socketRegistry,
            ILogger<IConcentratorDeduplication> logger)
        {
            this.cache = cache;
            this.deduplicationStrategy = deduplicationStrategy;
            this.socketRegistry = socketRegistry;
            this.logger = logger;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loRaRequest"></param>
        /// <param name="loRaDevice"></param>
        /// <returns><code>True</code> if this service should process this request and decide to drop or not.</returns>
        internal bool ShouldProcess(LoRaRequest loRaRequest, LoRaDevice loRaDevice)
            => (loRaRequest.Payload is LoRaPayloadData && this.deduplicationStrategy.Create(loRaDevice) is DeduplicationStrategyDrop)
               || loRaRequest.Payload is LoRaPayloadJoinRequest;

        public bool ShouldDrop(LoRaRequest loRaRequest, LoRaDevice loRaDevice)
        {
            if (!ShouldProcess(loRaRequest, loRaDevice))
                return false;

            var key = CreateCacheKey(loRaRequest);
            var stationEui = loRaRequest.StationEui;

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
                this.logger.LogDebug("Message received from the same EUI {StationEui} as before, will not drop.", stationEui);
                return false;
            }

            // received from a different station
            if (this.socketRegistry.IsSocketWriterOpen(previousStation))
            {
                this.logger.LogInformation($"{Constants.DuplicateMessageFromAnotherStationMsg} with EUI {stationEui}, will drop.");
                return true;
            }

            this.logger.LogInformation("Connectivity to previous station with EUI {PreviousStation}, was lost, will not drop and will use station with EUI {StationEui} from now onwards.",
                                       previousStation, stationEui);
            AddToCache(key, stationEui);
            return false;
        }

        internal static string CreateCacheKey(LoRaRequest loRaRequest)
            => loRaRequest.Payload switch
            {
                LoRaPayloadData asDataPayload => CreateCacheKeyCore(asDataPayload),
                LoRaPayloadJoinRequest asJoinPayload => CreateCacheKeyCore(asJoinPayload),
                _ => throw new ArgumentException($"{loRaRequest} with invalid type.")
            };

        private static string CreateCacheKeyCore(LoRaPayloadData payload)
        {
            var totalBufferLength = payload.DevAddr.Length + payload.Mic.Length + (payload.RawMessage?.Length ?? 0) + payload.Fcnt.Length;
            var buffer = totalBufferLength <= 128 ? stackalloc byte[totalBufferLength] : new byte[totalBufferLength]; // uses the stack for small allocations, otherwise the heap

            var index = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, BinaryPrimitives.ReadUInt32LittleEndian(payload.DevAddr.Span));
            index += payload.DevAddr.Length;
            //BinaryPrimitives.WriteUInt32LittleEndian(buffer[index..], BinaryPrimitives.ReadUInt32LittleEndian(payload.Mic.Span));
            //index += payload.Mic.Length;
            payload.RawMessage?.CopyTo(buffer[index..]);
            index += payload.RawMessage?.Length ?? 0;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[index..], BinaryPrimitives.ReadUInt16LittleEndian(payload.Fcnt.Span));

            var key = Sha256.ComputeHash(buffer.ToArray());

            return BitConverter.ToString(key);
        }

        private static string CreateCacheKeyCore(LoRaPayloadJoinRequest payload)
        {
            throw new NotImplementedException();
        }

        private void AddToCache(string key, StationEui stationEui)
            => this.cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = DefaultExpiration
            });

        public void Dispose() => this.cache.Dispose();
    }
}
