// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Security.Cryptography;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    internal sealed class ConcentratorDeduplication :
        IConcentratorDeduplication, IDisposable
    {
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache cache;
        private readonly WebSocketWriterRegistry<StationEui, string> socketRegistry;
        private readonly ILogger<IConcentratorDeduplication> logger;

        [ThreadStatic]
        private static SHA256? sha256;

        private static SHA256 Sha256 => sha256 ??= SHA256.Create();

        public ConcentratorDeduplication(
            IMemoryCache cache,
            WebSocketWriterRegistry<StationEui, string> socketRegistry,
            ILogger<IConcentratorDeduplication> logger)
        {
            this.cache = cache;
            this.socketRegistry = socketRegistry;
            this.logger = logger;
        }

        public bool ShouldDrop(LoRaRequest loRaRequest, LoRaDevice loRaDevice)
        {
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
            var totalBufferLength = DevAddr.Size + Mic.Size + payload.RawMessage.Length + sizeof(uint);
            var buffer = totalBufferLength <= 128 ? stackalloc byte[totalBufferLength] : new byte[totalBufferLength]; // uses the stack for small allocations, otherwise the heap
            var head = buffer; // keeps a view pointing at the start of the buffer

            //buffer = payload.DevAddr.Write(buffer);
            //buffer = updf.Mic.Write(buffer);
            //_ = Encoding.UTF8.GetBytes(updf.Payload, buffer);
            //BinaryPrimitives.WriteUInt16LittleEndian(buffer[updf.Payload.Length..], updf.Counter);

            var key = Sha256.ComputeHash(head.ToArray());

            return BitConverter.ToString(key);
        }

        private static string CreateCacheKeyCore(LoRaPayloadJoinRequest payload)
        {
            return payload.DevAddr.ToString();
        }

        private void AddToCache(string key, StationEui stationEui)
            => this.cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = DefaultExpiration
            });

        public void Dispose() => this.cache.Dispose();
    }
}
