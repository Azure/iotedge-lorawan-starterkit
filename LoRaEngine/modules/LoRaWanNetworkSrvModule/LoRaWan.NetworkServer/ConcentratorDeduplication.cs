// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Microsoft.Extensions.Caching.Memory;
    using System;

    public sealed class ConcentratorDeduplication : IConcentratorDeduplication, IDisposable
    {
        internal MemoryCache Cache { get; private set; }
        private static readonly TimeSpan CacheEntryExpiration = TimeSpan.FromHours(24);

        public ConcentratorDeduplication()
        {
            Cache = new MemoryCache(new MemoryCacheOptions());
        }

        public bool IsDuplicate(LoRaRequest request, uint payloadFrameCounterAdjusted, bool isRestartedDevice, string deviceEUI)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            DeviceEUICacheEntry previousMessageFromDevice;
            lock (Cache)
            {
                // we have not encountered this device ever, the device is restarted (-> counter was reset) or we received a newer message
                if (!Cache.TryGetValue(deviceEUI, out previousMessageFromDevice) || isRestartedDevice || payloadFrameCounterAdjusted > previousMessageFromDevice.FrameCounter)
                {
                    _ = Cache.Set(deviceEUI, new DeviceEUICacheEntry(payloadFrameCounterAdjusted, request.ConcentratorId), new MemoryCacheEntryOptions()
                    {
                        AbsoluteExpirationRelativeToNow = CacheEntryExpiration
                    });
                    return false;
                }
            }

            // we received a retry: only the concentrator that won the first time is allowed
            if (payloadFrameCounterAdjusted == previousMessageFromDevice.FrameCounter)
            {
                return request.ConcentratorId != previousMessageFromDevice.ConcentratorId;
            }

            // The case of payloadFrameCounterAdjusted < previousMessageFromDevice.FrameCounter is handled in the DefaultLoRaDataRequestHandler.ValidateRequest
            // For our purpose here, this is not a duplicate as the previous message may have been lost.
            return false;
        }

        public void Dispose() => Cache.Dispose();

        internal sealed class DeviceEUICacheEntry
        {
            public uint FrameCounter { get; private set; }
            public string ConcentratorId { get; private set; }

            public DeviceEUICacheEntry(uint frameCounter, string concentratorId)
            {
                FrameCounter = frameCounter;
                ConcentratorId = concentratorId;
            }
        }
    }
}
