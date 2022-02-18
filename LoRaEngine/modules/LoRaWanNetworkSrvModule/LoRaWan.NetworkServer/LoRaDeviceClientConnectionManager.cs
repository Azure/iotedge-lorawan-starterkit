// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Manages <see cref="ILoRaDeviceClient"/> connections for <see cref="LoRaDevice"/>.
    /// </summary>
    public sealed class LoRaDeviceClientConnectionManager : ILoRaDeviceClientConnectionManager
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

        public void Dispose()
        {
            foreach (var client in this.clientByDevEui.Values)
                client.Dispose();
        }

        public Task<T> UseAsync<T>(DevEui devEui, Func<ILoRaDeviceClient, Task<T>> processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));

            ILoRaDeviceClient client;
            lock (this.clientByDevEui)
                client = this.clientByDevEui[devEui];
            return processor(client);
        }

        public IAsyncDisposable ReserveConnection(DevEui devEui)
        {
            ILoRaDeviceClient client;
            lock (this.clientByDevEui)
                client = this.clientByDevEui[devEui];
            return ((LoRaDeviceClient)client).BeginDeviceClientConnectionActivity();
        }

        private sealed class LoRaDeviceClient : ILoRaDeviceClient
        {
            private readonly LoRaDeviceClientConnectionManager connectionManager;
            private readonly ILoRaDeviceClient client;
            private readonly LoRaDevice device;
            private int pid;
            private readonly ExclusiveProcessor<int> exclusiveProcessor = new();
            private bool shouldDisconnect;
            private int activities;

            public LoRaDeviceClient(LoRaDeviceClientConnectionManager connectionManager, ILoRaDeviceClient client, LoRaDevice device)
            {
                this.connectionManager = connectionManager;
                this.client = client;
                this.device = device;
            }

            public DevEui DevEui => this.device.DevEUI;
            public TimeSpan KeepAliveTimeout => TimeSpan.FromSeconds(this.device.KeepAliveTimeout);

            public void Dispose() => this.client?.Dispose();

            private Task<T> InvokeExclusivelyAsync<T>(Func<ILoRaDeviceClient, Task<T>> processor) =>
                InvokeExclusivelyAsync(doesNotRequireOpenConnection: false, processor);

            private async Task<T> InvokeExclusivelyAsync<T>(bool doesNotRequireOpenConnection, Func<ILoRaDeviceClient, Task<T>> processor)
            {
                _ = Interlocked.Increment(ref this.pid);
                return await this.exclusiveProcessor.ProcessAsync(this.pid, async () =>
                {
                    if (!doesNotRequireOpenConnection && this.device.KeepAliveTimeout > 0)
                    {
                        _ = EnsureConnected();
                        this.connectionManager.SetupSchedule(this);
                    }

                    return await processor(this.client);
                });
            }

            public Task<Twin> GetTwinAsync(CancellationToken cancellationToken) =>
                InvokeExclusivelyAsync(client => client.GetTwinAsync(cancellationToken));

            public Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties) =>
                InvokeExclusivelyAsync(client => client.SendEventAsync(telemetry, properties));

            public Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties, CancellationToken cancellationToken) =>
                InvokeExclusivelyAsync(client => client.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken));

            public Task<Message> ReceiveAsync(TimeSpan timeout) =>
                InvokeExclusivelyAsync(client => client.ReceiveAsync(timeout));

            public Task<bool> CompleteAsync(Message cloudToDeviceMessage) =>
                InvokeExclusivelyAsync(client => client.CompleteAsync(cloudToDeviceMessage));

            public Task<bool> AbandonAsync(Message cloudToDeviceMessage) =>
                InvokeExclusivelyAsync(client => client.AbandonAsync(cloudToDeviceMessage));

            public Task<bool> RejectAsync(Message cloudToDeviceMessage) =>
                InvokeExclusivelyAsync(client => client.RejectAsync(cloudToDeviceMessage));

            public bool EnsureConnected() =>
                this.client.EnsureConnected();

            public Task DisconnectAsync() =>
                DisconnectAsync(deferred: false);

            private enum DisconnectionResult { Disconnected, Deferred, AlreadyDisconnected }

            private Task<DisconnectionResult> DisconnectAsync(bool deferred) =>
                InvokeExclusivelyAsync(doesNotRequireOpenConnection: true, async client =>
                {
                    if (Interlocked.Add(ref this.activities, 0) > 0)
                    {
                        this.shouldDisconnect = true;
                        return DisconnectionResult.Deferred;
                    }

                    if (deferred && !this.shouldDisconnect)
                        return DisconnectionResult.AlreadyDisconnected;

                    this.shouldDisconnect = false;
                    await client.DisconnectAsync();
                    return DisconnectionResult.Disconnected;
                });

            public IAsyncDisposable BeginDeviceClientConnectionActivity()
            {
                _ = Interlocked.Increment(ref this.activities);
                return new AsyncDisposable(async cancellationToken =>
                {
                    if (Interlocked.Decrement(ref this.activities) > 0)
                        return;

                    _ = await DisconnectAsync(deferred: true);
                });
            }
        }
    }
}
