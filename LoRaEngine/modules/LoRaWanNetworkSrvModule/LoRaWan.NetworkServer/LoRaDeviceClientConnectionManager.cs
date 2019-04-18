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
    /// Manages <see cref="ILoRaDeviceClient"/> connections for <see cref="LoRaDevice"/>
    /// </summary>
    public class LoRaDeviceClientConnectionManager : ILoRaDeviceClientConnectionManager
    {
        public class ManagedConnection
        {
            public ManagedConnection(LoRaDevice loRaDevice, ILoRaDeviceClient deviceClient)
            {
                this.LoRaDevice = loRaDevice;
                this.DeviceClient = deviceClient;
            }

            public LoRaDevice LoRaDevice { get; }

            public ILoRaDeviceClient DeviceClient { get; }
        }

        IMemoryCache cache;
        ConcurrentDictionary<string, ManagedConnection> managedConnections = new ConcurrentDictionary<string, ManagedConnection>();

        public LoRaDeviceClientConnectionManager(IMemoryCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public bool EnsureConnected(LoRaDevice loRaDevice)
        {
            if (this.managedConnections.TryGetValue(this.GetConnectionCacheKey(loRaDevice.DevEUI), out var managedConnection))
            {
                if (loRaDevice.KeepAliveTimeout > 0)
                {
                    if (!managedConnection.DeviceClient.EnsureConnected())
                        return false;

                    this.SetupSchedule(managedConnection);
                }

                return true;
            }

            throw new ManagedConnectionException($"Connection for device {loRaDevice.DevEUI} was not found");
        }

        /// <summary>
        /// Sets the schedule for a device client disconnect
        /// </summary>
        private void SetupSchedule(ManagedConnection managedConnection)
        {
            var key = this.GetScheduleCacheKey(managedConnection.LoRaDevice.DevEUI);
            // Touching an existing item will update the last access item
            // Creating will start the expiration count
            this.cache.GetOrCreate(
                key,
                (ce) =>
                {
                    ce.SlidingExpiration = TimeSpan.FromSeconds(managedConnection.LoRaDevice.KeepAliveTimeout);
                    ce.RegisterPostEvictionCallback(this.OnScheduledDisconnect);

                    Logger.Log(managedConnection.LoRaDevice.DevEUI, $"client connection timeout set to {managedConnection.LoRaDevice.KeepAliveTimeout} seconds (sliding expiration)", LogLevel.Debug);

                    return managedConnection;
                });
        }

        void OnScheduledDisconnect(object key, object value, EvictionReason reason, object state)
        {
            var managedConnection = (ManagedConnection)value;

            if (!managedConnection.LoRaDevice.TryDisconnect())
            {
                Logger.Log(managedConnection.LoRaDevice.DevEUI, $"scheduled device disconnection has been postponed. Device client connection is active", LogLevel.Information);
                this.SetupSchedule(managedConnection);
            }
        }

        private string GetConnectionCacheKey(string devEUI) => string.Concat("connection:", devEUI);

        private string GetScheduleCacheKey(string devEUI) => string.Concat("connection:schedule:", devEUI);

        public ILoRaDeviceClient Get(LoRaDevice loRaDevice)
        {
            if (this.managedConnections.TryGetValue(this.GetConnectionCacheKey(loRaDevice.DevEUI), out var managedConnection))
                return managedConnection.DeviceClient;

            throw new ManagedConnectionException($"Connection for device {loRaDevice.DevEUI} was not found");
        }

        public void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient)
        {
            this.managedConnections.AddOrUpdate(
                this.GetConnectionCacheKey(loRaDevice.DevEUI),
                new ManagedConnection(loRaDevice, loraDeviceClient),
                (k, existing) =>
                {
                    // Update existing
                    return new ManagedConnection(loRaDevice, loraDeviceClient);
                });
        }

        public void Release(LoRaDevice loRaDevice)
        {
            if (this.managedConnections.TryRemove(this.GetConnectionCacheKey(loRaDevice.DevEUI), out var removedItem))
            {
                removedItem.DeviceClient.Dispose();
            }
        }

        /// <summary>
        /// Tries to trigger scanning of expired items
        /// For tests only
        /// </summary>
        public void TryScanExpiredItems()
        {
            this.cache.TryGetValue(string.Empty, out _);
        }
    }
}