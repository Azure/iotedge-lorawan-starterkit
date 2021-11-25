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
            try
            {
                await Task.Delay((int)this.options.ValidationInterval.TotalMilliseconds, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            OnRefresh();

            // remove any devices that were not seen for the configured amount of time
            RemoveExpiredDevices();

            // refresh the devices that were not refreshed within the configured time window
            await RefreshDevicesAsync(cancellationToken);

            // re-schedule
            _ = RefreshCacheAsync(cancellationToken);
        }

        private void RemoveExpiredDevices()
        {
            lock (this.syncLock)
            {
                var now = DateTimeOffset.UtcNow;
                var itemsToRemove = this.euiCache.Values.Where(x => now - x.LastSeen > this.options.MaxUnobservedLifetime);
                foreach (var expiredDevice in itemsToRemove)
                {
                    _ = Remove(expiredDevice);
                    expiredDevice.Dispose();
                }
            }
        }

        private async Task RefreshDevicesAsync(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
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
                    break;
                }
                catch (LoRaProcessingException ex)
                {
                    // retry on next iteration
                    this.logger.LogError(ex, "Failed to refresh device.");
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

                if (!string.IsNullOrEmpty(loRaDevice.DevAddr))
                {
                    if (this.devAddrCache.TryGetValue(loRaDevice.DevAddr, out var devicesByDevAddr))
                    {
                        result &= devicesByDevAddr.Remove(loRaDevice.DevEUI, out _);
                        if (devicesByDevAddr.IsEmpty)
                        {
                            result &= this.devAddrCache.Remove(loRaDevice.DevAddr, out _);
                        }
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
                if (!this.devAddrCache.TryGetValue(oldDevAddr, out var devicesByDevAddr))
                {
                    throw new InvalidOperationException($"The specified {nameof(oldDevAddr)}:{oldDevAddr} does not exist.");
                }

                if (devicesByDevAddr.TryGetValue(loRaDevice.DevEUI, out var device))
                {
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
                else
                {
                    throw new InvalidOperationException($"Device does not exist in cache with this {nameof(oldDevAddr)}:{oldDevAddr} and {nameof(loRaDevice.DevEUI)}:{loRaDevice.DevEUI}");
                }
            }
        }

        public virtual bool HasRegistrations(string devAddr)
        {
            return this.devAddrCache.TryGetValue(devAddr, out var items) && items.Any();
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
                if (!string.IsNullOrEmpty(loraDevice.DevAddr))
                {
                    var devAddrLookup = this.devAddrCache.GetOrAdd(loraDevice.DevAddr, (_) => new ConcurrentDictionary<string, LoRaDevice>());
                    devAddrLookup[loraDevice.DevEUI] = loraDevice;
                }
                this.euiCache[loraDevice.DevEUI] = loraDevice;
                loraDevice.LastSeen = DateTimeOffset.UtcNow;
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
            return loRaDevice != null;
        }

        private void TrackCacheStats(LoRaDevice? device)
        {
            if (device is { })
            {
                TrackHit(device);
            }
            else
            {
                TrackMiss();
            }
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
            return loRaDevice != null;
        }

        public CacheStatistics CalculateStatistics() => new CacheStatistics(this.statisticsTracker.Hit, this.statisticsTracker.Miss, this.euiCache.Count);

        private void TrackMiss()
        {
            this.statisticsTracker.IncrementMiss();
        }

        private void TrackHit(LoRaDevice loRaDevice)
        {
            loRaDevice.LastSeen = DateTimeOffset.UtcNow;
            this.statisticsTracker.IncrementHit();
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

    public record CacheStatistics(int Hit, int Miss, int Count);

    public record LoRaDeviceCacheOptions
    {
        public TimeSpan ValidationInterval { get; set; }
        public TimeSpan MaxUnobservedLifetime { get; set; }
        public TimeSpan RefreshInterval { get; set; }
    }
}
