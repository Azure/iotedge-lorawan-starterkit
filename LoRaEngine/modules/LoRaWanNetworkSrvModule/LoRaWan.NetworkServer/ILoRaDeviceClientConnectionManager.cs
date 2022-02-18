// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    public interface ILoRaDeviceClientConnectionManager : IDisposable
    {
        ILoRaDeviceClient GetClient(LoRaDevice loRaDevice);

        void Release(LoRaDevice loRaDevice);

        void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient);

        Task<T> UseAsync<T>(DevEui devEui, Func<ILoRaDeviceClient, Task<T>> processor);

        IAsyncDisposable ReserveConnection(DevEui devEui);
    }

    public static class LoRaDeviceClientConnectionManagerExtensions
    {
        public static Task UseAsync(this ILoRaDeviceClientConnectionManager connectionManager, DevEui devEui, Func<ILoRaDeviceClient, Task> processor)
        {
            if (connectionManager == null) throw new ArgumentNullException(nameof(connectionManager));

            return connectionManager.UseAsync(devEui, async client =>
            {
                await processor(client);
                return 0;
            });
        }
    }

    public sealed partial class LoRaDeviceClientConnectionManager
    {
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
                InvokeExclusivelyAsync(false, processor);

            private async Task<T> InvokeExclusivelyAsync<T>(bool isConnectionRequired, Func<ILoRaDeviceClient, Task<T>> processor)
            {
                _ = Interlocked.Increment(ref this.pid);
                return await this.exclusiveProcessor.ProcessAsync(this.pid, async () =>
                {
                    if (isConnectionRequired)
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
                InvokeExclusivelyAsync(isConnectionRequired: false, async client =>
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
