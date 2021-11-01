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

            lock (this.Cache) // TODO: more fine grained lock
            {
                _ = this.Cache.TryGetValue(deviceEUI, out CacheEntry latestMessage);

                if (latestMessage == null || latestMessage.FrameCounter < payloadFrameCounterAdjusted)
                {
                    // we have not seen this device before or we received a newer message
                    _ = this.Cache.Set(deviceEUI, new CacheEntry(payloadFrameCounterAdjusted, request.ConcentratorId));
                    return false;
                }

                if (latestMessage.FrameCounter == payloadFrameCounterAdjusted && latestMessage.ConcentratorId == request.ConcentratorId)
                {
                    // this is a retry
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

        private class CacheEntry
        {
            public uint FrameCounter { get; set; }
            public string ConcentratorId { get; set; }

            public CacheEntry(uint frameCounter, string concentratorId)
            {
                FrameCounter = frameCounter;
                ConcentratorId = concentratorId;
            }
        }
    }
}
