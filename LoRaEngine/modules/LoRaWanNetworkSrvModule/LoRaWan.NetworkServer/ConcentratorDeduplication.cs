// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Microsoft.Extensions.Caching.Memory;
    using System;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication, IDisposable
    {
        private readonly MemoryCache Cache;

        public ConcentratorDeduplication()
        {
            this.Cache = new MemoryCache(new MemoryCacheOptions());
        }

        public bool IsDuplicate(LoRaRequest request, uint payloadFrameCounterAdjusted, bool isRestartedDevice, string deviceEUI)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            lock (this.Cache)
            {
                // we have not encountered this device ever, the device is restarted (-> counter was reset) or we received a newer message or the device
                if (!this.Cache.TryGetValue(deviceEUI, out CacheEntry previousMessage) || isRestartedDevice || payloadFrameCounterAdjusted > previousMessage.FrameCounter)
                {
                    _ = this.Cache.Set(deviceEUI, new CacheEntry(payloadFrameCounterAdjusted, request.ConcentratorId), new MemoryCacheEntryOptions()
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    });
                    return false;
                }

                // we received a retry: only the concentrator that won the first time is allowed
                if (payloadFrameCounterAdjusted == previousMessage.FrameCounter && request.ConcentratorId == previousMessage.ConcentratorId)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        public void Dispose() => this.Cache.Dispose();

        private sealed class CacheEntry
        {
            public uint FrameCounter { get; private set; }
            public string ConcentratorId { get; private set; }

            public CacheEntry(uint frameCounter, string concentratorId)
            {
                FrameCounter = frameCounter;
                ConcentratorId = concentratorId;
            }
        }
    }
}
