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

        public bool IsDuplicate(LoRaRequest request, uint payloadFrameCounterAdjusted, string deviceEUI)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            lock (this.Cache)
            {
                if (!this.Cache.TryGetValue(deviceEUI, out CacheEntry previousMessage) || payloadFrameCounterAdjusted > previousMessage.FrameCounter)
                {
                    // we have not encountered this device before or we received a newer message
                    _ = this.Cache.Set(deviceEUI, new CacheEntry(payloadFrameCounterAdjusted, request.ConcentratorId));
                    return false;
                }

                if (payloadFrameCounterAdjusted == previousMessage.FrameCounter && request.ConcentratorId == previousMessage.ConcentratorId)
                {
                    // this is a retry from the same concentrator
                    return false;
                }
                else
                {
                    // we received an older message from any concentrator or the same message but from a different concentrator
                    return true;
                }
            }
        }

        public void Dispose() => Cache.Dispose();

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
