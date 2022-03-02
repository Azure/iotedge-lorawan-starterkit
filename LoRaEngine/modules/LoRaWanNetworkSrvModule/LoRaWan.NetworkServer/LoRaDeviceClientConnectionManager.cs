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
    using LoRaTools;

    /// <summary>
    /// Manages <see cref="ILoRaDeviceClient"/> connections for <see cref="LoRaDevice"/>.
    /// </summary>
    public sealed class LoRaDeviceClientConnectionManager : ILoRaDeviceClientConnectionManager
    {
        private readonly IMemoryCache cache;
        private readonly ILoggerFactory? loggerFactory;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<DevEui, SynchronizedLoRaDeviceClient> clientByDevEui = new();

        /// <remarks>
        /// This record could be a "record struct" but since it is used as a key in a
        /// <see cref="IMemoryCache"/>, which treats all keys as objects, it will always get boxed.
        /// As a result, it might as well be kept as a reference to avoid superfluous allocations
        /// (via unboxing and re-boxing).
        /// </remarks>
        private record ScheduleKey(DevEui DevEui);

        public LoRaDeviceClientConnectionManager(IMemoryCache cache,
                                                 ILogger<LoRaDeviceClientConnectionManager> logger) :
            this(cache, null, logger)
        { }

        [ActivatorUtilitiesConstructor]
        public LoRaDeviceClientConnectionManager(IMemoryCache cache,
                                                 ILoggerFactory? loggerFactory,
                                                 ILogger<LoRaDeviceClientConnectionManager> logger)
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
                            await client.DisconnectAsync(CancellationToken.None);
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

        public async Task ReleaseAsync(LoRaDevice loRaDevice)
        {
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));

            if (this.clientByDevEui.TryRemove(loRaDevice.DevEUI, out var removedItem))
            {
                await removedItem.DisposeAsync();
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

        public async ValueTask DisposeAsync()
        {
            await this.clientByDevEui.Values.DisposeAllAsync(20);
        }

        public IAsyncDisposable BeginDeviceClientConnectionActivity(LoRaDevice loRaDevice)
        {
            ArgumentNullException.ThrowIfNull(loRaDevice, nameof(loRaDevice));
            return this.clientByDevEui[loRaDevice.DevEUI].BeginDeviceClientConnectionActivity();
        }

        private sealed class SynchronizedLoRaDeviceClient : ILoRaDeviceClient, ILoRaDeviceClientSynchronizedOperationEventSource, IIdentityProvider<ILoRaDeviceClient>
        {
            private readonly ILoRaDeviceClient client;
            private readonly LoRaDevice device;
            private readonly ILogger? logger;
            private int operationSequenceNumber;
            private readonly ExclusiveProcessor<Process> exclusiveProcessor = new();
            private bool disconnectedDuringActivity;
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

            ILoRaDeviceClient IIdentityProvider<ILoRaDeviceClient>.Identity => this.client;

            public ValueTask DisposeAsync() => this.client.DisposeAsync();

            public event EventHandler? EnsureConnectedSucceeded;

            private Task<T> InvokeExclusivelyAsync<T>(Func<ILoRaDeviceClient, Task<T>> processor,
                                                      [CallerMemberName] string? callerName = null) =>
                InvokeExclusivelyAsync(doesNotRequireOpenConnection: false, processor, callerName);

            private async Task<T> InvokeExclusivelyAsync<T>(bool doesNotRequireOpenConnection,
                                                            Func<ILoRaDeviceClient, Task<T>> processor,
                                                            [CallerMemberName] string? callerName = null)
            {
                _ = Interlocked.Increment(ref this.operationSequenceNumber);
                return await this.exclusiveProcessor.ProcessAsync(new Process(this.operationSequenceNumber, callerName!), async () =>
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

            public Task DisconnectAsync(CancellationToken cancellationToken) =>
                DisconnectAsync(isActivityEnding: false, cancellationToken);

            private enum DisconnectionResult { Disconnected, Deferred, NotDisconnected }

            /// <remarks>
            /// This method is either called by an explicit call to <see cref="DisconnectAsync"/>
            /// or when an activity is ending (when <see cref="isActivityEnding"/> is <c>true</c>)
            /// to issue a disconnection if it was deferred and no other activities are
            /// outstanding.
            /// </remarks>
            private Task<DisconnectionResult> DisconnectAsync(bool isActivityEnding, CancellationToken cancellationToken) =>
                InvokeExclusivelyAsync(doesNotRequireOpenConnection: true, async client =>
                {
                    DisconnectionResult result;

                    if (Interlocked.Add(ref this.activities, 0) > 0)
                    {
                        this.disconnectedDuringActivity = true;
                        result = DisconnectionResult.Deferred;
                    }
                    else if (isActivityEnding && !this.disconnectedDuringActivity)
                    {
                        result = DisconnectionResult.NotDisconnected;
                    }
                    else
                    {
                        this.disconnectedDuringActivity = false;
                        await client.DisconnectAsync(cancellationToken);
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

                    _ = await DisconnectAsync(isActivityEnding: true, cancellationToken);
                });
            }

            private OperationEvent<Process>? submittedEvent;
            private OperationEvent<Process>? processingEvent;
            private OperationEvent<(Process, ExclusiveProcessor<Process>.IProcessingOutcome)>? processedEvent;

            private OperationEvent<Process> SubmittedEvent =>
                this.submittedEvent ??= new(this, p => p, static (ep, h) => ep.Submitted += h, static (ep, h) => ep.Submitted -= h);

            private OperationEvent<Process> ProcessingEvent =>
                this.processingEvent ??= new(this, p => p, static (ep, h) => ep.Processing += h, static (ep, h) => ep.Processing -= h);

            private OperationEvent<(Process, ExclusiveProcessor<Process>.IProcessingOutcome)> ProcessedEvent =>
                this.processedEvent ??= new(this, args => args.Item1, static (ep, h) => ep.Processed += h, static (ep, h) => ep.Processed -= h);

            event EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs>? ILoRaDeviceClientSynchronizedOperationEventSource.Queued
            {
                add => SubmittedEvent.Add(value);
                remove => SubmittedEvent.Remove(value);
            }

            event EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs>? ILoRaDeviceClientSynchronizedOperationEventSource.Processing
            {
                add => ProcessingEvent.Add(value);
                remove => ProcessingEvent.Remove(value);
            }

            event EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs>? ILoRaDeviceClientSynchronizedOperationEventSource.Processed
            {
                add => ProcessedEvent.Add(value);
                remove => ProcessedEvent.Remove(value);
            }

            private sealed class OperationEvent<T>
            {
                private EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs>? field;
                private readonly SynchronizedLoRaDeviceClient client;
                private readonly EventHandler<T> handler;
                private readonly Action<ExclusiveProcessor<Process>, EventHandler<T>> adder;
                private readonly Action<ExclusiveProcessor<Process>, EventHandler<T>> remover;

                public OperationEvent(SynchronizedLoRaDeviceClient client,
                                      Func<T, Process> argsMapper,
                                      Action<ExclusiveProcessor<Process>, EventHandler<T>> adder,
                                      Action<ExclusiveProcessor<Process>, EventHandler<T>> remover)
                {
                    this.client = client;
                    this.handler = (_, args) =>
                    {
                        var (id, name) = argsMapper(args);
                        this.field?.Invoke(this.client, new LoRaDeviceClientSynchronizedOperationEventArgs(id, name));
                    };
                    this.adder = adder;
                    this.remover = remover;
                }

                public void Add(EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs>? value)
                {
                    if (this.field is not null)
                        this.remover(this.client.exclusiveProcessor, this.handler);
                    this.adder(this.client.exclusiveProcessor, this.handler);
                    this.field = value;
                }

                public void Remove(EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs>? value)
                {
                    if (value != this.field)
                        return;
                    this.field = null;
                    this.remover(this.client.exclusiveProcessor, this.handler);
                }
            }
        }
    }

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public sealed record LoRaDeviceClientSynchronizedOperationEventArgs(int Id, string Name);
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix

    public interface ILoRaDeviceClientSynchronizedOperationEventSource
    {
        event EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs> Queued;
        event EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs> Processing;
        event EventHandler<LoRaDeviceClientSynchronizedOperationEventArgs> Processed;
    }
}
