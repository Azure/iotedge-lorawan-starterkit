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
            var canceled = false;
            while (!canceled)
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
                        canceled = true;
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

        public virtual bool Remove(LoRaDevice loRaDevice)
        {
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));

            var result = true;

            lock (this.syncLock)
            {
                result &= this.euiCache.Remove(loRaDevice.DevEUI, out _);

                if (!string.IsNullOrEmpty(loRaDevice.DevAddr) &&
                     this.devAddrCache.TryGetValue(loRaDevice.DevAddr, out var devicesByDevAddr))
                {
                    result &= devicesByDevAddr.Remove(loRaDevice.DevEUI, out _);
                    if (devicesByDevAddr.IsEmpty)
                    {
                        result &= this.devAddrCache.Remove(loRaDevice.DevAddr, out _);
                    }
                }
            }
            return result;
        }

        public void CleanupOldDevAddrForDevice(LoRaDevice loRaDevice, string oldDevAddr)
        {
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));
            _ = oldDevAddr ?? throw new ArgumentNullException(nameof(oldDevAddr));
            if (loRaDevice.DevAddr == oldDevAddr) throw new InvalidOperationException($"The old devAddr '{oldDevAddr}' to be removed, is still active.");

            lock (this.syncLock)
            {
                if (!this.devAddrCache.TryGetValue(oldDevAddr, out var devicesByDevAddr) ||
                    !devicesByDevAddr.TryGetValue(loRaDevice.DevEUI, out var device))
                {
                    throw new InvalidOperationException($"Device does not exist in cache with this {nameof(oldDevAddr)}:{oldDevAddr} and {nameof(loRaDevice.DevEUI)}:{loRaDevice.DevEUI}");
                }

                if (!ReferenceEquals(device, loRaDevice))
                {
                    throw new InvalidOperationException($"Device does not match exist device in cache with this {nameof(oldDevAddr)}:{oldDevAddr} and {nameof(loRaDevice.DevEUI)}:{loRaDevice.DevEUI}");
                }

                _ = devicesByDevAddr.TryRemove(loRaDevice.DevEUI, out _);
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

        public void Register(LoRaDevice loraDevice)
        {
            _ = loraDevice ?? throw new ArgumentNullException(nameof(loraDevice));

            lock (this.syncLock)
            {
                if (this.euiCache.TryGetValue(loraDevice.DevEUI, out var existingDevice))
                {
                    // joins do register without DevAddr and then re-register with the DevAddr
                    // however, the references need to match
                    if (!ReferenceEquals(existingDevice, loraDevice))
                    {
                        throw new InvalidOperationException($"{loraDevice.DevEUI} already registered. We would overwrite a device client");
                    }
                }
                else
                {
                    this.euiCache[loraDevice.DevEUI] = loraDevice;
                }

                if (!string.IsNullOrEmpty(loraDevice.DevAddr))
                {
                    var devAddrLookup = this.devAddrCache.GetOrAdd(loraDevice.DevAddr, (_) => new ConcurrentDictionary<string, LoRaDevice>());
                    devAddrLookup[loraDevice.DevEUI] = loraDevice;
                }
                loraDevice.LastSeen = DateTimeOffset.UtcNow;
                this.logger.LogDebug($"Device registered in cache.");
            }
        }

        public void Reset()
        {
            CleanupAllDevices();
        }

        public virtual bool TryGetForPayload(LoRaPayload payload, [MaybeNullWhen(returnValue: false)] out LoRaDevice loRaDevice)
        {
            _ = payload ?? throw new ArgumentNullException(nameof(payload));

            loRaDevice = null;

            var devAddr = ConversionHelper.ByteArrayToString(payload.DevAddr);
            lock (this.syncLock)
            {
                if (this.devAddrCache.TryGetValue(devAddr, out var devices))
                {
                    loRaDevice = devices.Values.FirstOrDefault(x => !string.IsNullOrEmpty(x.NwkSKey) && ValidateMic(x, payload));
                }
            }

            TrackCacheStats(loRaDevice);
            return loRaDevice is not null;
        }

        protected virtual bool ValidateMic(LoRaDevice loRaDevice, LoRaPayload loRaPayload)
        {
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));
            _ = loRaPayload ?? throw new ArgumentNullException(nameof(loRaPayload));
            return loRaDevice.ValidateMic(loRaPayload);
        }

        public bool TryGetByDevEui(string devEUI, [MaybeNullWhen(returnValue: false)] out LoRaDevice loRaDevice)
        {
            lock (this.syncLock)
            {
                _ = this.euiCache.TryGetValue(devEUI, out loRaDevice);
            }

            TrackCacheStats(loRaDevice);
            return loRaDevice is not null;
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
