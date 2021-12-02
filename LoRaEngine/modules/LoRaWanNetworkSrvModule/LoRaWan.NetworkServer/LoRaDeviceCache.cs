// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class LoRaDeviceCache : IDisposable
    {
        private readonly LoRaDeviceCacheOptions options;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, LoRaDevice>> devAddrCache;
        private readonly ConcurrentDictionary<string, LoRaDevice> euiCache;
        private readonly object syncLock = new object();
        private readonly NetworkServerConfiguration configuration;
        private readonly ILogger<LoRaDeviceCache> logger;
        private CancellationTokenSource? ctsDispose;
        private readonly StatisticsTracker statisticsTracker = new StatisticsTracker();

        protected LoRaDeviceCache(LoRaDeviceCacheOptions options, NetworkServerConfiguration configuration, ILogger<LoRaDeviceCache> logger, CancellationToken externalRefreshCancellationToken)
        {
            this.options = options;
            this.devAddrCache = new ConcurrentDictionary<string, ConcurrentDictionary<string, LoRaDevice>>();
            this.euiCache = new ConcurrentDictionary<string, LoRaDevice>();
            this.ctsDispose = externalRefreshCancellationToken.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(externalRefreshCancellationToken) : new CancellationTokenSource();

            _ = RefreshCacheAsync(this.ctsDispose.Token);

            this.configuration = configuration;
            this.logger = logger;
        }

        public LoRaDeviceCache(LoRaDeviceCacheOptions options, NetworkServerConfiguration configuration, ILogger<LoRaDeviceCache> logger)
            : this(options, configuration, logger, CancellationToken.None)
        { }

        protected virtual void OnRefresh() { }

        private async Task RefreshCacheAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.options.ValidationInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                OnRefresh();

                // remove any devices that were not seen for the configured amount of time

                var now = DateTimeOffset.UtcNow;

                lock (this.syncLock)
                {
                    var itemsToRemove = this.euiCache.Values.Where(x => now - x.LastSeen > this.options.MaxUnobservedLifetime);
                    foreach (var expiredDevice in itemsToRemove)
                    {
                        _ = Remove(expiredDevice);
                        expiredDevice.Dispose();
                    }
                }

                // refresh the devices that were not refreshed within the configured time window

                var itemsToRefresh = this.euiCache.Values.Where(x => now - x.LastUpdate > this.options.RefreshInterval).ToList();
                var tasks = new List<Task>(itemsToRefresh.Count);

                foreach (var item in itemsToRefresh)
                {
                    tasks.Add(RefreshDeviceAsync(item, cancellationToken));
                }

                while (tasks.Count > 0)
                {
                    var t = await Task.WhenAny(tasks);
                    _ = tasks.Remove(t);

                    try
                    {
                        await t;
                    }
                    catch (TaskCanceledException)
                    {
                        // ignore canceled task for now (so all remaining tasks can be awaited),
                        // but outer loop will eventually break due to cancellation
                    }
                    catch (LoRaProcessingException ex)
                    {
                        // retry on next iteration
                        this.logger.LogError(ex, "Failed to refresh device.");
                    }
                }
            }
        }

        protected virtual Task RefreshDeviceAsync(LoRaDevice device, CancellationToken cancellationToken)
        {
            _ = device ?? throw new ArgumentNullException(nameof(device));
            return device.InitializeAsync(this.configuration, cancellationToken);
        }

        public bool Remove(string devEui)
        {
            lock (this.syncLock)
            {
                return TryGetByDevEui(devEui, out var device) && Remove(device);
            }
        }

        public virtual bool Remove(LoRaDevice device)
        {
            _ = device ?? throw new ArgumentNullException(nameof(device));

            var result = true;

            lock (this.syncLock)
            {
                result &= this.euiCache.Remove(device.DevEUI, out _);

                if (!string.IsNullOrEmpty(device.DevAddr) &&
                     this.devAddrCache.TryGetValue(device.DevAddr, out var devicesByDevAddr))
                {
                    result &= devicesByDevAddr.Remove(device.DevEUI, out _);
                    if (devicesByDevAddr.IsEmpty)
                    {
                        result &= this.devAddrCache.Remove(device.DevAddr, out _);
                    }
                }
            }
            return result;
        }

        public void CleanupOldDevAddrForDevice(LoRaDevice device, string oldDevAddr)
        {
            _ = device ?? throw new ArgumentNullException(nameof(device));
            _ = oldDevAddr ?? throw new ArgumentNullException(nameof(oldDevAddr));
            if (device.DevAddr == oldDevAddr) throw new InvalidOperationException($"The old devAddr '{oldDevAddr}' to be removed, is still active.");

            lock (this.syncLock)
            {
                if (!this.devAddrCache.TryGetValue(oldDevAddr, out var devicesByDevAddr) ||
                    !devicesByDevAddr.TryGetValue(device.DevEUI, out var cachedDevice))
                {
                    throw new InvalidOperationException($"Device does not exist in cache with this {nameof(oldDevAddr)}:{oldDevAddr} and {nameof(device.DevEUI)}:{device.DevEUI}");
                }

                if (!ReferenceEquals(cachedDevice, device))
                {
                    throw new InvalidOperationException($"Device does not match exist device in cache with this {nameof(oldDevAddr)}:{oldDevAddr} and {nameof(device.DevEUI)}:{device.DevEUI}");
                }

                _ = devicesByDevAddr.TryRemove(device.DevEUI, out _);
                if (devicesByDevAddr.IsEmpty)
                {
                    _ = this.devAddrCache.TryRemove(oldDevAddr, out _);
                }
            }

            this.logger.LogDebug($"previous device devAddr ({oldDevAddr}) removed from cache.");
        }

        public virtual bool HasRegistrations(string devAddr)
        {
            return RegistrationCount(devAddr) > 0;
        }

        public virtual bool HasRegistrationsForOtherGateways(string devAddr)
        {
            return this.devAddrCache.TryGetValue(devAddr, out var items) && items.Any(x => !x.Value.IsOurDevice);
        }

        public int RegistrationCount(string devAddr)
        {
            return this.devAddrCache.TryGetValue(devAddr, out var items) ? items.Count : 0;
        }

        public void Register(LoRaDevice device)
        {
            _ = device ?? throw new ArgumentNullException(nameof(device));

            lock (this.syncLock)
            {
                if (this.euiCache.TryGetValue(device.DevEUI, out var existingDevice))
                {
                    // joins do register without DevAddr and then re-register with the DevAddr
                    // however, the references need to match
                    if (!ReferenceEquals(existingDevice, device))
                    {
                        throw new InvalidOperationException($"{device.DevEUI} already registered. We would overwrite a device client");
                    }
                }
                else
                {
                    this.euiCache[device.DevEUI] = device;
                }

                if (!string.IsNullOrEmpty(device.DevAddr))
                {
                    var devAddrLookup = this.devAddrCache.GetOrAdd(device.DevAddr, (_) => new ConcurrentDictionary<string, LoRaDevice>());
                    devAddrLookup[device.DevEUI] = device;
                }
                device.LastSeen = DateTimeOffset.UtcNow;
                this.logger.LogDebug($"Device registered in cache.");
            }
        }

        public void Reset()
        {
            CleanupAllDevices();
        }

        public virtual bool TryGetForPayload(LoRaPayload payload, [MaybeNullWhen(returnValue: false)] out LoRaDevice device)
        {
            _ = payload ?? throw new ArgumentNullException(nameof(payload));

            device = null;

            var devAddr = ConversionHelper.ByteArrayToString(payload.DevAddr);
            lock (this.syncLock)
            {
                if (this.devAddrCache.TryGetValue(devAddr, out var devices))
                {
                    device = devices.Values.FirstOrDefault(x => !string.IsNullOrEmpty(x.NwkSKey) && ValidateMic(x, payload));
                }
            }

            TrackCacheStats(device);
            return device is not null;
        }

        protected virtual bool ValidateMic(LoRaDevice device, LoRaPayload loRaPayload)
        {
            _ = device ?? throw new ArgumentNullException(nameof(device));
            _ = loRaPayload ?? throw new ArgumentNullException(nameof(loRaPayload));
            return device.ValidateMic(loRaPayload);
        }

        public bool TryGetByDevEui(string devEUI, [MaybeNullWhen(returnValue: false)] out LoRaDevice device)
        {
            lock (this.syncLock)
            {
                _ = this.euiCache.TryGetValue(devEUI, out device);
            }

            TrackCacheStats(device);
            return device is not null;
        }

        public CacheStatistics CalculateStatistics() => new CacheStatistics(this.statisticsTracker.Hit, this.statisticsTracker.Miss, this.euiCache.Count);

        private void TrackCacheStats(LoRaDevice? device)
        {
            if (device is not null)
            {
                device.LastSeen = DateTimeOffset.UtcNow;
                this.statisticsTracker.IncrementHit();
            }
            else
            {
                this.statisticsTracker.IncrementMiss();
            }
        }

        private void CleanupAllDevices()
        {
            lock (this.syncLock)
            {
                foreach (var device in this.euiCache.Values)
                {
                    device.Dispose();
                }
                this.euiCache.Clear();
                this.devAddrCache.Clear();
                this.logger.LogInformation($"{nameof(LoRaDeviceCache)} cleared.");
            }
        }

        private class StatisticsTracker
        {
            private int hit;
            private int miss;

            internal int Hit => this.hit;
            internal int Miss => this.miss;

            internal void IncrementHit() => Interlocked.Increment(ref hit);
            internal void IncrementMiss() => Interlocked.Increment(ref miss);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                lock (this.syncLock)
                {
                    this.ctsDispose?.Cancel();
                    this.ctsDispose?.Dispose();
                    this.ctsDispose = null;
                    CleanupAllDevices();
                }
            }
        }
    }

    public sealed record CacheStatistics(int Hit, int Miss, int Count);

    public sealed record LoRaDeviceCacheOptions
    {
        /// <summary>
        /// How often the validation of the device cache is run.
        /// The local device cache will be evaluated after the specified
        /// time and all devices that need to be removed or
        /// refreshed are processed.
        /// </summary>
        public TimeSpan ValidationInterval { get; init; }

        /// <summary>
        /// The time we allow a device to be not sending any
        /// messages, before we consider it stale and remove
        /// it from the cache. This should be a high value,
        /// as LoRa sensors are optimized for saving power and
        /// might only send a message, once a week for example.
        /// </summary>
        public TimeSpan MaxUnobservedLifetime { get; init; }

        /// <summary>
        /// If a device is in cache and it exeeds this time
        /// since the last refresh, a twin refresh is triggered
        /// and the cached data is updated.
        /// </summary>
        public TimeSpan RefreshInterval { get; init; }
    }
}
