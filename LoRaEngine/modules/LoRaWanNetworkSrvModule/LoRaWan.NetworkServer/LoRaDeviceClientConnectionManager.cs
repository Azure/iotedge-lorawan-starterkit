// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Manages <see cref="ILoRaDeviceClient"/> connections for <see cref="LoRaDevice"/>.
    /// </summary>
    public sealed partial class LoRaDeviceClientConnectionManager : ILoRaDeviceClientConnectionManager
    {
        private readonly IMemoryCache cache;
        private readonly ILogger<LoRaDeviceClientConnectionManager> logger;
        private readonly ConcurrentDictionary<DevEui, LoRaDeviceClient> clientByDevEui = new();

        public LoRaDeviceClientConnectionManager(IMemoryCache cache, ILogger<LoRaDeviceClientConnectionManager> logger)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sets the schedule for a device client disconnect.
        /// </summary>
        private void SetupSchedule(LoRaDeviceClient client)
        {
            var key = GetScheduleCacheKey(client.DevEui);
            // Touching an existing item will update the last access item
            // Creating will start the expiration count
            _ = this.cache.GetOrCreate(
                    key,
                    ce =>
                    {
                        var keepAliveTimeout = client.KeepAliveTimeout;
                        ce.SlidingExpiration = keepAliveTimeout;
                        _ = ce.RegisterPostEvictionCallback((_, value, _, _) => _ = ((LoRaDeviceClient)value).DisconnectAsync());
                        this.logger.LogDebug($"client connection timeout set to {keepAliveTimeout.TotalSeconds} seconds (sliding expiration)");
                        return client;
                    });
        }

        private static string GetScheduleCacheKey(DevEui devEui) => string.Concat("connection:schedule:", devEui);

        public ILoRaDeviceClient GetClient(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            return this.clientByDevEui.TryGetValue(loRaDevice.DevEUI, out var client)
                 ? client
                 : throw new ManagedConnectionException($"Connection for device {loRaDevice.DevEUI} was not found");
        }

        public void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            var key = loRaDevice.DevEUI;
            lock (this.clientByDevEui)
            {
                if (this.clientByDevEui.ContainsKey(key))
                {
                    throw new InvalidOperationException($"Connection already registered for device {loRaDevice.DevEUI}");
                }

                this.clientByDevEui[key] = new LoRaDeviceClient(this, loraDeviceClient, loRaDevice);
            }
        }

        public void Release(LoRaDevice loRaDevice)
        {
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));

            if (this.clientByDevEui.TryRemove(loRaDevice.DevEUI, out var removedItem))
            {
                removedItem.Dispose();
            }
        }

        /// <summary>
        /// Tries to trigger scanning of expired items
        /// For tests only.
        /// </summary>
        public void TryScanExpiredItems()
        {
            _ = this.cache.TryGetValue(string.Empty, out _);
        }
    }
}
