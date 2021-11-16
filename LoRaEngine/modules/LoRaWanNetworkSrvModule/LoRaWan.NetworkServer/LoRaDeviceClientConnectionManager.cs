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
        private readonly ConcurrentDictionary<string, ManagedConnection> managedConnections = new ConcurrentDictionary<string, ManagedConnection>();

        public LoRaDeviceClientConnectionManager(IMemoryCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public bool EnsureConnected(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            if (this.managedConnections.TryGetValue(GetConnectionCacheKey(loRaDevice.DevEUI), out var managedConnection))
            {
                if (loRaDevice.KeepAliveTimeout > 0)
                {
                    if (!managedConnection.DeviceClient.EnsureConnected())
                        return false;

                    SetupSchedule(managedConnection);
                }

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
                        ce.SlidingExpiration = TimeSpan.FromSeconds(managedConnection.LoRaDevice.KeepAliveTimeout);
                        _ = ce.RegisterPostEvictionCallback(OnScheduledDisconnect);

                        TcpLogger.Log(managedConnection.LoRaDevice.DevEUI, $"client connection timeout set to {managedConnection.LoRaDevice.KeepAliveTimeout} seconds (sliding expiration)", LogLevel.Debug);

                        return managedConnection;
                    });
        }

        private void OnScheduledDisconnect(object key, object value, EvictionReason reason, object state)
        {
            var managedConnection = (ManagedConnection)value;

            if (!managedConnection.LoRaDevice.TryDisconnect())
            {
                TcpLogger.Log(managedConnection.LoRaDevice.DevEUI, $"scheduled device disconnection has been postponed. Device client connection is active", LogLevel.Information);
                SetupSchedule(managedConnection);
            }
        }

        private static string GetConnectionCacheKey(string devEUI) => string.Concat("connection:", devEUI);

        private static string GetScheduleCacheKey(string devEUI) => string.Concat("connection:schedule:", devEUI);

        public ILoRaDeviceClient GetClient(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));
            if (this.managedConnections.TryGetValue(GetConnectionCacheKey(loRaDevice.DevEUI), out var managedConnection))
                return managedConnection.DeviceClient;

            throw new ManagedConnectionException($"Connection for device {loRaDevice.DevEUI} was not found");
        }

        public void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            this.managedConnections[GetConnectionCacheKey(loRaDevice.DevEUI)] = new ManagedConnection(loRaDevice, loraDeviceClient);
        }

        public void Release(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            if (this.managedConnections.TryRemove(GetConnectionCacheKey(loRaDevice.DevEUI), out var removedItem))
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
