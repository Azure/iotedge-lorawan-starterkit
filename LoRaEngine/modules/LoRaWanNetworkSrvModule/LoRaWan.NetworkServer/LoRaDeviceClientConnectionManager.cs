// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Manages <see cref="ILoRaDeviceClient"/> connections for <see cref="LoRaDevice"/>.
    /// </summary>
    public sealed class LoRaDeviceClientConnectionManager : ILoRaDeviceClientConnectionManager
    {
        internal sealed class ManagedConnection : IDisposable
        {
            public ManagedConnection(LoRaDevice loRaDevice, ILoRaDeviceClient deviceClient)
            {
                LoRaDevice = loRaDevice;
                DeviceClient = deviceClient;
            }

            public LoRaDevice LoRaDevice { get; }

            public ILoRaDeviceClient DeviceClient { get; }

            public void Dispose()
            {
                DeviceClient.Dispose();

                // Disposing the Connection Manager should only happen on application shutdown
                // (which in turn triggers the disposal of all managed connections).
                // In that specific case disposing the LoRaDevice will cause the LoRa device to unregister itself again,
                // which causes DeviceClient.Dispose() to be called twice. We do not optimize this case, since the Dispose logic is idempotent.
                LoRaDevice.Dispose();
            }
        }

        private readonly IMemoryCache cache;
        private readonly ILogger<LoRaDeviceClientConnectionManager> logger;
        private readonly ConcurrentDictionary<string, ManagedConnection> managedConnections = new ConcurrentDictionary<string, ManagedConnection>();

        public LoRaDeviceClientConnectionManager(IMemoryCache cache, ILogger<LoRaDeviceClientConnectionManager> logger)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool EnsureConnected(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            if (this.managedConnections.TryGetValue(GetConnectionCacheKey(loRaDevice.DevEUI), out var managedConnection))
            {
                if (!managedConnection.DeviceClient.EnsureConnected())
                    return false;

                SetupSchedule(managedConnection);

                return true;
            }

            throw new ManagedConnectionException($"Connection for device {loRaDevice.DevEUI} was not found");
        }

        /// <summary>
        /// Sets the schedule for a device client disconnect.
        /// </summary>
        private void SetupSchedule(ManagedConnection managedConnection)
        {
            var key = GetScheduleCacheKey(managedConnection.LoRaDevice.DevEUI);
            // Touching an existing item will update the last access item
            // Creating will start the expiration count
            _ = this.cache.GetOrCreate(
                    key,
                    (ce) =>
                    {
                        ce.SlidingExpiration = TimeSpan.FromSeconds(15);
                        _ = ce.RegisterPostEvictionCallback(OnScheduledDisconnect);

                        this.logger.LogDebug($"client connection timeout set to {managedConnection.LoRaDevice.KeepAliveTimeout} seconds (sliding expiration)");

                        return managedConnection;
                    });
        }

        private void OnScheduledDisconnect(object key, object value, EvictionReason reason, object state)
        {
            var managedConnection = (ManagedConnection)value;

            using var scope = this.logger.BeginDeviceScope(managedConnection.LoRaDevice.DevEUI);

            if (!managedConnection.LoRaDevice.TryDisconnect())
            {
                this.logger.LogInformation("scheduled device disconnection has been postponed. Device client connection is active");
                SetupSchedule(managedConnection);
            }
        }

        private static string GetConnectionCacheKey(DevEui devEui) => string.Concat("connection:", devEui);

        private static string GetScheduleCacheKey(DevEui devEui) => string.Concat("connection:schedule:", devEui);

        public ILoRaDeviceClient GetClient(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));
            return GetClient(loRaDevice.DevEUI);
        }

        public ILoRaDeviceClient GetClient(DevEui devEui)
        {
            if (this.managedConnections.TryGetValue(GetConnectionCacheKey(devEui), out var managedConnection))
                return managedConnection.DeviceClient;

            throw new ManagedConnectionException($"Connection for device {devEui} was not found");
        }

        public void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            var key = GetConnectionCacheKey(loRaDevice.DevEUI);
            lock (this.managedConnections)
            {
                if (this.managedConnections.ContainsKey(key))
                {
                    throw new InvalidOperationException($"Connection already registered for device {loRaDevice.DevEUI}");
                }

                this.managedConnections[key] = new ManagedConnection(loRaDevice, loraDeviceClient);
            }
        }

        public void Release(LoRaDevice loRaDevice)
        {
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));
            Release(loRaDevice.DevEUI);
        }

        public void Release(DevEui devEui)
        {
            if (this.managedConnections.TryRemove(GetConnectionCacheKey(devEui), out var removedItem))
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

        public void Dispose()
        {
            // The LoRaDeviceClientConnectionManager does not own the cache, but it owns all the managed connections.

            foreach (var it in this.managedConnections)
            {
                it.Value.Dispose();
            }
        }
    }
}
