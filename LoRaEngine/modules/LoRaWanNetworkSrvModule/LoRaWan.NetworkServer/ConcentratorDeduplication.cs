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
        public enum Result
        {
            NotDuplicate,
            DuplicateDueToResubmission,
            Duplicate,
            SoftDuplicate // detected as a duplicate but due to the DeduplicationStrategy marked as a "soft" duplicate
        }

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

        private bool ShouldDrop(LoRaRequest loRaRequest, LoRaDevice? loRaDevice)
            => loRaRequest.Payload is LoRaPayloadJoinRequest
            || (loRaRequest.Payload is LoRaPayloadData && this.deduplicationStrategy.Create(loRaDevice) is DeduplicationStrategyDrop);

        internal static bool RequiresConfirmation(LoRaRequest loraRequest)
            => loraRequest.Payload is LoRaPayloadData payload && (payload.IsConfirmed || payload.IsMacAnswerRequired);

        public Result CheckDuplicate(LoRaRequest loRaRequest, LoRaDevice? loRaDevice)
        {
            _ = loRaRequest ?? throw new ArgumentNullException(nameof(loRaRequest));

            var key = CreateCacheKey(loRaRequest);
            var stationEui = loRaRequest.StationEui;

            StationEui previousStation;
            lock (this.cache)
            {
                if (!this.cache.TryGetValue(key, out previousStation))
                {
                    AddToCache(key, stationEui);
                    return Result.NotDuplicate;
                }
            }

            if (RequiresConfirmation(loRaRequest) && previousStation == stationEui)
            {
                this.logger.LogDebug($"Message received from the same EUI {stationEui} as before, will not drop.");
                return Result.DuplicateDueToResubmission;
            }

            // received from a different station
            if (ShouldDrop(loRaRequest, loRaDevice))
            {
                this.logger.LogInformation($"{Constants.DuplicateMessageFromAnotherStationMsg} with EUI {stationEui}, will drop.");
                return Result.Duplicate;
            }

            this.logger.LogDebug($"Message from station with EUI {stationEui} will not be dropped due to DeduplicationStrategy.");
            return Result.SoftDuplicate;
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

            if (!payload.Mic.IsEmpty)
                BinaryPrimitives.WriteUInt32LittleEndian(buffer[index..], BinaryPrimitives.ReadUInt32LittleEndian(payload.Mic.Span));
            index += payload.Mic.Length;

            payload.RawMessage?.CopyTo(buffer[index..]);
            index += payload.RawMessage?.Length ?? 0;

            BinaryPrimitives.WriteUInt16LittleEndian(buffer[index..], BinaryPrimitives.ReadUInt16LittleEndian(payload.Fcnt.Span));

            var key = Sha256.ComputeHash(buffer.ToArray());

            return BitConverter.ToString(key);
        }

        private static string CreateCacheKeyCore(LoRaPayloadJoinRequest payload)
        {
            var joinEui = JoinEui.Read(payload.AppEUI.Span);
            var devEui = DevEui.Read(payload.DevEUI.Span);
            var devNonce = DevNonce.Read(payload.DevNonce.Span);

            var totalBufferLength = JoinEui.Size + DevEui.Size + DevNonce.Size;
            Span<byte> buffer = stackalloc byte[totalBufferLength];
            var head = buffer; // keeps a view pointing at the start of the buffer

            buffer = joinEui.Write(buffer);
            buffer = devEui.Write(buffer);
            buffer = devNonce.Write(buffer);

            var key = Sha256.ComputeHash(head.ToArray());

            return BitConverter.ToString(key);
        }

        private void AddToCache(string key, StationEui stationEui)
            => this.cache.Set(key, stationEui, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = DefaultExpiration
            });
    }
}
