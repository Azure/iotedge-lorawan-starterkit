// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    public sealed class ConcentratorDeduplication :
        IConcentratorDeduplication, IDisposable
    {
        public enum Result
        {
            NotDuplicate,
            DuplicateDueToResubmission,
            SoftDuplicate, // detected as a duplicate but due to the DeduplicationStrategy marked as a "soft" duplicate
            Duplicate
        }

        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache cache;
        private readonly SemaphoreSlim cacheSemaphore = new SemaphoreSlim(1);

        private readonly ILogger<IConcentratorDeduplication> logger;

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

        public async Task<Result> CheckDuplicateAsync(LoRaRequest loRaRequest, LoRaDevice? loRaDevice)
        {
            _ = loRaRequest ?? throw new ArgumentNullException(nameof(loRaRequest));
            if (loRaDevice is null && loRaRequest.Payload is LoRaPayloadData)
                throw new ArgumentNullException(nameof(loRaDevice));

            var key = CreateCacheKey(loRaRequest);
            var stationEui = loRaRequest.StationEui;

            StationEui previousStation;
            await this.cacheSemaphore.WaitAsync();
            try
            {
                if (!this.cache.TryGetValue(key, out previousStation))
                {
                    _ = this.cache.Set(key, stationEui, new MemoryCacheEntryOptions()
                    {
                        SlidingExpiration = DefaultExpiration
                    });
                    return Result.NotDuplicate;
                }
            }
            finally
            {
                _ = this.cacheSemaphore.Release();
            }

            if (LoRaRequest.RequiresConfirmation(loRaRequest) && previousStation == stationEui)
            {
                this.logger.LogDebug($"Message was received previously from the same EUI {stationEui} (\"confirmedResubmit\").");
                return Result.DuplicateDueToResubmission;
            }

            // received from a different station
            if (ShouldDrop(loRaRequest, loRaDevice))
            {
                this.logger.LogDebug($"{Constants.DuplicateMessageFromAnotherStationMsg} with EUI {stationEui}.");
                return Result.Duplicate;
            }

            this.logger.LogDebug($"Message from station with EUI {stationEui} marked as soft duplicate due to DeduplicationStrategy.");
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

        private static bool ShouldDrop(LoRaRequest loRaRequest, LoRaDevice? loRaDevice)
            => loRaRequest.Payload is LoRaPayloadJoinRequest || loRaDevice?.Deduplication is DeduplicationMode.Drop;

        public void Dispose()
            => this.cacheSemaphore.Dispose();
    }
}
