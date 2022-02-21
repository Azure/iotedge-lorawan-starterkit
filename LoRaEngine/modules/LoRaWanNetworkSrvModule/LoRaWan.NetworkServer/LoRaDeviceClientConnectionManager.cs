// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public sealed record LoRaDeviceClientSynchronizedEventArgs(int Id, string Name);
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix

    public interface ILoRaDeviceClientSynchronizedEventSource
    {
        event EventHandler<LoRaDeviceClientSynchronizedEventArgs> Queued;
        event EventHandler<LoRaDeviceClientSynchronizedEventArgs> Processing;
        event EventHandler<LoRaDeviceClientSynchronizedEventArgs> Processed;
    }

    /// <summary>
    /// Manages <see cref="ILoRaDeviceClient"/> connections for <see cref="LoRaDevice"/>.
    /// </summary>
    public sealed class LoRaDeviceClientConnectionManager : ILoRaDeviceClientConnectionManager
    {
        private readonly IMemoryCache cache;
        private readonly ILoggerFactory? loggerFactory;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<DevEui, SynchronizedLoRaDeviceClient> clientByDevEui = new();

        private record struct ScheduleKey(DevEui DevEui);

        public LoRaDeviceClientConnectionManager(IMemoryCache cache,
                                                 ILogger<LoRaDeviceClientConnectionManager> logger) :
            this(cache, null, logger)
        { }

        [ActivatorUtilitiesConstructor]
        public LoRaDeviceClientConnectionManager(IMemoryCache cache,
                                                 ILoggerFactory? loggerFactory,
                                                 ILogger<ILoRaDeviceClientConnectionManager> logger)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.loggerFactory = loggerFactory;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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

            var loRaDeviceClient = new SynchronizedLoRaDeviceClient(loraDeviceClient, loRaDevice,
                                                                    this.loggerFactory?.CreateLogger<SynchronizedLoRaDeviceClient>());

            loRaDeviceClient.EnsureConnectedSucceeded += (sender, args) =>
            {
                var client = (SynchronizedLoRaDeviceClient)sender!;

                if (loRaDeviceClient.KeepAliveTimeout <= TimeSpan.Zero)
                    return;

                // Set the schedule for a device client disconnect.
                // Touching an existing item will update the last access item
                // and creating will start the expiration count.

                _ = this.cache.GetOrCreate(new ScheduleKey(client.DevEui), ce =>
                {
                    var keepAliveTimeout = client.KeepAliveTimeout;
                    ce.SlidingExpiration = keepAliveTimeout;
                    // NOTE! Use of an async void, while generally discouraged, is used here
                    // intentionally since the eviction callback is synchronous and using async
                    // void logically equates to "Task.Run". The risk of any exception going
                    // unnoticed (in either case) is mitigated by logging it.
                    _ = ce.RegisterPostEvictionCallback(state: this.logger, callback: static async (_, value, _, state) =>
                    {
                        var client = (SynchronizedLoRaDeviceClient)value;
                        var logger = (ILogger)state;
                        try
                        {
                            using var scope = logger.BeginDeviceScope(client.DevEui);
                            await client.DisconnectAsync();
                        }
                        catch (Exception ex) when (ExceptionFilterUtility.False(() => logger.LogError(ex, "Error while disconnecting client ({DevEUI}) due to cache eviction.", client.DevEui)))
                        {
                        }
                    });

                    this.logger.LogDebug($"client connection timeout set to {keepAliveTimeout.TotalSeconds} seconds (sliding expiration)");
                    return client;
                });
            };

            var key = loRaDevice.DevEUI;
            if (!this.clientByDevEui.TryAdd(key, loRaDeviceClient))
                throw new InvalidOperationException($"Connection already registered for device {key}");
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

        public IAsyncDisposable BeginDeviceClientConnectionActivity(LoRaDevice loRaDevice)
        {
            if (loRaDevice == null) throw new ArgumentNullException(nameof(loRaDevice));
            return this.clientByDevEui[loRaDevice.DevEUI].BeginDeviceClientConnectionActivity();
        }

        private sealed class SynchronizedLoRaDeviceClient : ILoRaDeviceClient, ILoRaDeviceClientSynchronizedEventSource
        {
            private readonly ILoRaDeviceClient client;
            private readonly LoRaDevice device;
            private readonly ILogger? logger;
            private int pid;
            private readonly ExclusiveProcessor<Process> exclusiveProcessor = new();
            private bool shouldDisconnect;
            private int activities;

            private record struct Process(int Id, string Name);

            public SynchronizedLoRaDeviceClient(ILoRaDeviceClient client, LoRaDevice device, ILogger<SynchronizedLoRaDeviceClient>? logger)
            {
                this.client = client;
                this.device = device;
                this.logger = logger;

                if (logger is { } someLogger && someLogger.IsEnabled(LogLevel.Debug))
                {
                    this.exclusiveProcessor.Submitted += (_, p) =>
                    {
                        var (id, name) = p;
                        someLogger.LogDebug(@"Queued ""{Name}"" ({Id})", name, id);
                    };

                    this.exclusiveProcessor.Processing += (_, p) =>
                    {
                        var (id, name) = p;
                        someLogger.LogDebug(@"Invoking ""{Name}"" ({Id})", name, id);
                    };

                    this.exclusiveProcessor.Processed += (_, args) =>
                    {
                        var ((id, name), outcome) = args;
                        someLogger.LogDebug(@"Invoked ""{Name}"" ({Id}); status = {Status}, run-time = {RunTime}, wait-time = {WaitTime}",
                                            name, id, outcome.Task.Status, outcome.RunDuration, outcome.WaitDuration);
                    };

                    this.exclusiveProcessor.Interrupted += (_, p) =>
                    {
                        var (interrupted, interrupting) = p;
                        someLogger.LogDebug(@"Interrupted ""{Name}"" ({Id}) by {InterruptingName} ({InterruptingId})",
                                            interrupted.Name, interrupted.Id,
                                            interrupting.Name, interrupting.Id);
                    };
                }
            }

            public DevEui DevEui => this.device.DevEUI;
            public TimeSpan KeepAliveTimeout => TimeSpan.FromSeconds(this.device.KeepAliveTimeout);

            public void Dispose() => this.client?.Dispose();

            public event EventHandler? EnsureConnectedSucceeded;

            private Task<T> InvokeExclusivelyAsync<T>(Func<ILoRaDeviceClient, Task<T>> processor,
                                                      [CallerMemberName] string? callerName = null) =>
                InvokeExclusivelyAsync(doesNotRequireOpenConnection: false, processor, callerName);

            private async Task<T> InvokeExclusivelyAsync<T>(bool doesNotRequireOpenConnection,
                                                            Func<ILoRaDeviceClient, Task<T>> processor,
                                                            [CallerMemberName] string? callerName = null)
            {
                _ = Interlocked.Increment(ref this.pid);
                return await this.exclusiveProcessor.ProcessAsync(new Process(this.pid, callerName!), async () =>
                {
                    if (!doesNotRequireOpenConnection)
                        _ = EnsureConnected();

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

            public bool EnsureConnected()
            {
                var connected = this.client.EnsureConnected();

                if (this.logger?.IsEnabled(LogLevel.Debug) ?? false)
                {
                    const string message = $"{nameof(EnsureConnected)} = {{Connected}}";
                    this.logger.LogDebug(message, connected);
                }

                if (connected)
                    EnsureConnectedSucceeded?.Invoke(this, EventArgs.Empty);

                return connected;
            }

            public Task DisconnectAsync() =>
                DisconnectAsync(deferred: false);

            private enum DisconnectionResult { Disconnected, Deferred, NotDisconnected }

            private Task<DisconnectionResult> DisconnectAsync(bool deferred) =>
                InvokeExclusivelyAsync(doesNotRequireOpenConnection: true, async client =>
                {
                    DisconnectionResult result;

                    if (Interlocked.Add(ref this.activities, 0) > 0)
                    {
                        this.shouldDisconnect = true;
                        result = DisconnectionResult.Deferred;
                    }
                    else if (deferred && !this.shouldDisconnect)
                    {
                        result = DisconnectionResult.NotDisconnected;
                    }
                    else
                    {
                        this.shouldDisconnect = false;
                        await client.DisconnectAsync();
                        result = DisconnectionResult.Disconnected;
                    }

                    if (this.logger is { } logger && logger.IsEnabled(LogLevel.Debug))
                    {
                        const string message = $"{nameof(DisconnectAsync)} = {{DisconnectResult}}";
                        logger.LogDebug(message, result);
                    }

                    return result;
                });

            public IAsyncDisposable BeginDeviceClientConnectionActivity()
            {
                var activities = Interlocked.Increment(ref this.activities);

                if (this.logger is { } logger && logger.IsEnabled(LogLevel.Debug))
                {
                    const string message = $"{nameof(BeginDeviceClientConnectionActivity)} = {{Activities}}";
                    logger.LogDebug(message, activities);
                }

                return new AsyncDisposable(async cancellationToken =>
                {
                    var activities = Interlocked.Decrement(ref this.activities);

                    if (this.logger is { } logger && logger.IsEnabled(LogLevel.Debug))
                    {
                        const string message = $"{nameof(BeginDeviceClientConnectionActivity)} (disposal) = {{Activities}}";
                        logger.LogDebug(message, activities);
                    }

                    if (activities > 0)
                        return;

                    _ = await DisconnectAsync(deferred: true);
                });
            }

            private EventHandler<LoRaDeviceClientSynchronizedEventArgs>? submittedEventHandler;
            private EventHandler<LoRaDeviceClientSynchronizedEventArgs>? processingEventHandler;
            private EventHandler<LoRaDeviceClientSynchronizedEventArgs>? processedEventHandler;

            event EventHandler<LoRaDeviceClientSynchronizedEventArgs>? ILoRaDeviceClientSynchronizedEventSource.Queued
            {
                add
                {
                    if (this.submittedEventHandler is not null)
                        this.exclusiveProcessor.Submitted -= OnSubmitted;
                    this.exclusiveProcessor.Submitted += OnSubmitted;
                    this.submittedEventHandler = value;
                }
                remove
                {
                    if (value != this.processingEventHandler)
                        return;
                    this.submittedEventHandler = null;
                    this.exclusiveProcessor.Submitted -= OnSubmitted;
                }
            }

            private void OnSubmitted(object? sender, Process process) =>
                this.submittedEventHandler?.Invoke(this, new LoRaDeviceClientSynchronizedEventArgs(process.Id, process.Name));

            event EventHandler<LoRaDeviceClientSynchronizedEventArgs>? ILoRaDeviceClientSynchronizedEventSource.Processing
            {
                add
                {
                    if (this.processedEventHandler is not null)
                        this.exclusiveProcessor.Processing -= OnProcessing;
                    this.exclusiveProcessor.Processing += OnProcessing;
                    this.processingEventHandler = value;
                }
                remove
                {
                    if (value != this.processingEventHandler)
                        return;
                    this.processingEventHandler = null;
                    this.exclusiveProcessor.Processing -= OnProcessing;
                }
            }

            private void OnProcessing(object? sender, Process process) =>
                this.processingEventHandler?.Invoke(this, new LoRaDeviceClientSynchronizedEventArgs(process.Id, process.Name));

            event EventHandler<LoRaDeviceClientSynchronizedEventArgs>? ILoRaDeviceClientSynchronizedEventSource.Processed
            {
                add
                {
                    if (this.processedEventHandler is not null)
                        this.exclusiveProcessor.Processed -= OnProcessed;
                    this.exclusiveProcessor.Processed += OnProcessed;
                    this.processedEventHandler = value;
                }
                remove
                {
                    if (value != this.processedEventHandler)
                        return;
                    this.processedEventHandler = null;
                    this.exclusiveProcessor.Processed -= OnProcessed;
                }
            }

            private void OnProcessed(object? sender, (Process, ExclusiveProcessor<Process>.IProcessingOutcome) args)
            {
                var ((id, name), _) = args;
                this.processedEventHandler?.Invoke(this, new LoRaDeviceClientSynchronizedEventArgs(id, name));
            }
        }
    }
}
