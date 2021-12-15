// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using System.Security.Cryptography;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    public sealed class ConcentratorDeduplication :
        IConcentratorDeduplication
    {
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);
        private readonly IMemoryCache cache;
        private readonly ILogger<IConcentratorDeduplication> logger;
        private static readonly object cacheLock = new object();

        [ThreadStatic]
        private static SHA256? sha256;

        private static SHA256 Sha256 => sha256 ??= SHA256.Create();

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

            this.logger.LogDebug($"{Constants.DuplicateMessageFromAnotherStationMsg} with EUI {loRaRequest.StationEui}.");
            return ConcentratorDeduplicationResult.Duplicate;
        }

        public ConcentratorDeduplicationResult CheckDuplicateData(LoRaRequest loRaRequest, LoRaDevice loRaDevice)
        {
            _ = loRaRequest ?? throw new ArgumentNullException(nameof(loRaRequest));
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));
            var key = CreateCacheKey((LoRaPayloadData)loRaRequest.Payload);
            if (EnsureFirstMessageInCache(key, loRaRequest, out var previousStation))
                return ConcentratorDeduplicationResult.NotDuplicate;

            if (previousStation == loRaRequest.StationEui)
            {
                this.logger.LogDebug($"Message was received previously from the same EUI {loRaRequest.StationEui} (\"confirmedResubmit\").");
                return ConcentratorDeduplicationResult.DuplicateDueToResubmission;
            }

            if (loRaDevice.Deduplication == DeduplicationMode.Drop)
            {
                this.logger.LogDebug($"{Constants.DuplicateMessageFromAnotherStationMsg} with EUI {loRaRequest.StationEui}.");
                return ConcentratorDeduplicationResult.Duplicate;
            }

            this.logger.LogDebug($"Message from station with EUI {loRaRequest.StationEui} marked as soft duplicate due to DeduplicationStrategy.");
            return ConcentratorDeduplicationResult.DuplicateAllowUpstream;
        }

        private bool EnsureFirstMessageInCache(string key, LoRaRequest loRaRequest, out StationEui previousStation)
        {
            var stationEui = loRaRequest.StationEui;

            lock (cacheLock)
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

        internal static string CreateCacheKey(LoRaRequest loRaRequest)
            => loRaRequest.Payload switch
            {
                LoRaPayloadData asDataPayload => CreateCacheKey(asDataPayload),
                LoRaPayloadJoinRequest asJoinPayload => CreateCacheKey(asJoinPayload),
                _ => throw new ArgumentException($"Provided request is of type {loRaRequest.GetType()} which is not valid for deduplication.")
            };

        private static string CreateCacheKey(LoRaPayloadData payload)
        {
            var totalBufferLength = payload.DevAddr.Length + payload.Mic.Length + (payload.RawMessage?.Length ?? 0) + payload.Fcnt.Length;
            var buffer = totalBufferLength <= 128 ? stackalloc byte[totalBufferLength] : new byte[totalBufferLength]; // uses the stack for small allocations, otherwise the heap

            var index = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, BinaryPrimitives.ReadUInt32LittleEndian(payload.DevAddr.Span));
            index += payload.DevAddr.Length;

            if (!payload.Mic.IsEmpty)
                BinaryPrimitives.WriteUInt32LittleEndian(buffer[index..], BinaryPrimitives.ReadUInt32LittleEndian(payload.Mic.Span));
            index += payload.Mic.Length;

            payload.RawMessage?.CopyTo(buffer[index..]);
            index += payload.RawMessage?.Length ?? 0;

            BinaryPrimitives.WriteUInt16LittleEndian(buffer[index..], BinaryPrimitives.ReadUInt16LittleEndian(payload.Fcnt.Span));

            var key = Sha256.ComputeHash(buffer.ToArray());

            return BitConverter.ToString(key);
        }

        private static string CreateCacheKey(LoRaPayloadJoinRequest payload)
        {
            var joinEui = JoinEui.Read(payload.AppEUI.Span);
            var devEui = DevEui.Read(payload.DevEUI.Span);
            var devNonce = DevNonce.Read(payload.DevNonce.Span);

            var totalBufferLength = JoinEui.Size + DevEui.Size + DevNonce.Size;
            Span<byte> buffer = stackalloc byte[totalBufferLength];
            var head = buffer; // keeps a view pointing at the start of the buffer

            buffer = joinEui.Write(buffer);
            buffer = devEui.Write(buffer);
            _ = devNonce.Write(buffer);

            var key = Sha256.ComputeHash(head.ToArray());

            return BitConverter.ToString(key);
        }
    }
}
