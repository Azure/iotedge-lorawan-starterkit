// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// LoRa device registry.
    /// </summary>
    public sealed class LoRaDeviceRegistry : ILoRaDeviceRegistry
    {
        // Caches a device making join for 30 minutes
        private const int INTERVAL_TO_CACHE_DEVICE_IN_JOIN_PROCESS_IN_MINUTES = 30;

        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceFactory deviceFactory;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;
        private readonly NetworkServerConfiguration configuration;
        private readonly object getOrCreateLoadingDevicesRequestQueueLock;
        private readonly object getOrCreateJoinDeviceLoaderLock;

        private readonly IMemoryCache cache;
        private readonly LoRaDeviceCache deviceCache;

        /// <summary>
        /// Gets or sets the interval in which devices will be loaded
        /// Only affect reload attempts for same DevAddr.
        /// </summary>
        public TimeSpan DevAddrReloadInterval { get; set; }

        public LoRaDeviceRegistry(
            NetworkServerConfiguration configuration,
            IMemoryCache cache,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceFactory deviceFactory,
            LoRaDeviceCache deviceCache)
        {
            this.configuration = configuration;
            this.cache = cache;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.initializers = new HashSet<ILoRaDeviceInitializer>();
            DevAddrReloadInterval = TimeSpan.FromSeconds(30);
            this.getOrCreateLoadingDevicesRequestQueueLock = new object();
            this.getOrCreateJoinDeviceLoaderLock = new object();
            this.deviceCache = deviceCache;
        }

        /// <summary>
        /// Registers a <see cref="ILoRaDeviceInitializer"/>.
        /// </summary>
        public void RegisterDeviceInitializer(ILoRaDeviceInitializer initializer) => this.initializers.Add(initializer);

        private DeviceLoaderSynchronizer GetOrCreateLoadingDevicesRequestQueue(string devAddr)
        {
            // Need to get and ensure it has started since the GetOrAdd can create multiple objects
            // https://github.com/aspnet/Extensions/issues/708
            lock (this.getOrCreateLoadingDevicesRequestQueueLock)
            {
                return this.cache.GetOrCreate(
                    $"devaddrloader:{devAddr}",
                    (ce) =>
                    {
                        var cts = new CancellationTokenSource();
                        ce.ExpirationTokens.Add(new CancellationChangeToken(cts.Token));

                        var loader = new DeviceLoaderSynchronizer(
                            devAddr,
                            this.loRaDeviceAPIService,
                            this.deviceFactory,
                            this.deviceCache,
                            this.initializers);

                        _ = loader.LoadAsync().ContinueWith((t) =>
                        {
                            // If the operation to load was successfull
                            // wait for 30 seconds for pending requests to go through and avoid additional calls
                            if (t.IsCompletedSuccessfully && !loader.HasLoadingDeviceError && DevAddrReloadInterval > TimeSpan.Zero)
                            {
                                // remove from cache after 30 seconds
                                cts.CancelAfter(DevAddrReloadInterval);
                            }
                            else
                            {
                                // remove from cache now
                                cts.Cancel();
                            }
                        }, TaskScheduler.Default);

                        return loader;
                    });
            }
        }

        public ILoRaDeviceRequestQueue GetLoRaRequestQueue(LoRaRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (this.deviceCache.TryGetForPayload(request.Payload, out var cachedDevice))
            {
                Logger.Log(cachedDevice.DevEUI, "device in cache", LogLevel.Debug);
                if (cachedDevice.IsOurDevice)
                {
                    return cachedDevice;
                }

                return new ExternalGatewayLoRaRequestQueue(cachedDevice);
            }

            // not in cache, need to make a single search by dev addr
            return GetOrCreateLoadingDevicesRequestQueue(ConversionHelper.ByteArrayToString(request.Payload.DevAddr));
        }

        /// <summary>
        /// Gets a device by DevEUI.
        /// </summary>
        public async Task<LoRaDevice> GetDeviceByDevEUIAsync(string devEUI)
        {
            if (this.deviceCache.TryGetByDevEui(devEUI, out var cachedDevice))
                return cachedDevice;

            var searchResult = await this.loRaDeviceAPIService.SearchByEuiAsync(DevEui.Parse(devEUI));
            if (searchResult == null || searchResult.Count == 0)
                return null;

            var firstDevice = searchResult[0];
            var loRaDevice = await this.deviceFactory.CreateAndRegisterAsync(firstDevice, CancellationToken.None);

            if (string.IsNullOrEmpty(loRaDevice.DevAddr))
            {
                // not joined yet - this is an invalid device
                _ = this.deviceCache.Remove(devEUI);
                loRaDevice.Dispose();
                return null;
            }

            if (this.initializers != null)
            {
                foreach (var initializers in this.initializers)
                {
                    initializers.Initialize(loRaDevice);
                }
            }

            return loRaDevice;
        }

        // Creates cache key for join device loader: "joinloader:{devEUI}"
        private static string GetJoinDeviceLoaderCacheKey(string devEUI) => string.Concat("joinloader:", devEUI);

        // Removes join device loader from cache
        private void RemoveJoinDeviceLoader(string devEUI) => this.cache.Remove(GetJoinDeviceLoaderCacheKey(devEUI));

        // Gets or adds a join device loader to the memory cache
        private JoinDeviceLoader GetOrCreateJoinDeviceLoader(IoTHubDeviceInfo ioTHubDeviceInfo)
        {
            // Need to get and ensure it has started since the GetOrAdd can create multiple objects
            // https://github.com/aspnet/Extensions/issues/708
            lock (this.getOrCreateJoinDeviceLoaderLock)
            {
                return this.cache.GetOrCreate(GetJoinDeviceLoaderCacheKey(ioTHubDeviceInfo.DevEUI), (entry) =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(INTERVAL_TO_CACHE_DEVICE_IN_JOIN_PROCESS_IN_MINUTES);
                    return new JoinDeviceLoader(ioTHubDeviceInfo, this.deviceFactory);
                });
            }
        }

        /// <summary>
        /// Searchs for devices that match the join request.
        /// </summary>
        public async Task<LoRaDevice> GetDeviceForJoinRequestAsync(string devEUI, string devNonce)
        {
            if (string.IsNullOrEmpty(devEUI) || string.IsNullOrEmpty(devNonce))
            {
                Logger.Log(devEUI, "join refused: missing devEUI/AppEUI/DevNonce in request", LogLevel.Error);
                return null;
            }

            Logger.Log(devEUI, "querying the registry for OTAA device", LogLevel.Debug);

            var searchDeviceResult = await this.loRaDeviceAPIService.SearchAndLockForJoinAsync(
                gatewayID: this.configuration.GatewayID,
                devEUI: devEUI,
                devNonce: devNonce);

            if (searchDeviceResult.IsDevNonceAlreadyUsed)
            {
                Logger.Log(devEUI, $"join refused: Join already processed by another gateway.", LogLevel.Information);
                return null;
            }

            if (searchDeviceResult?.Devices == null || searchDeviceResult.Devices.Count == 0)
            {
                var msg = searchDeviceResult.RefusedMessage ?? "join refused: no devices found matching join request";
                Logger.Log(devEUI, msg, LogLevel.Information);
                return null;
            }

            var matchingDeviceInfo = searchDeviceResult.Devices[0];

            if (deviceCache.TryGetByDevEui(matchingDeviceInfo.DevEUI, out var cachedDevice))
            {
                return cachedDevice;
            }

            var loader = GetOrCreateJoinDeviceLoader(matchingDeviceInfo);
            var loRaDevice = await loader.LoadAsync();
            if (!loader.CanCache)
                RemoveJoinDeviceLoader(devEUI);

            return loRaDevice;
        }

        /// <summary>
        /// Updates a device after a successful login.
        /// </summary>
        public void UpdateDeviceAfterJoin(LoRaDevice loRaDevice, string oldDevAddr)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            // once added, call initializers
            foreach (var initializer in this.initializers)
                initializer.Initialize(loRaDevice);

            // make sure the device is also added to the devAddr
            // cache after the DevAddr was generated.
            this.deviceCache.Register(loRaDevice);

            CleanupOldDevAddr(loRaDevice, oldDevAddr);
        }

        private void CleanupOldDevAddr(LoRaDevice loRaDevice, string oldDevAddr)
        {
            if (string.IsNullOrEmpty(oldDevAddr) || loRaDevice.DevAddr == oldDevAddr)
            {
                return;
            }

            this.deviceCache.CleanupOldDevAddrForDevice(loRaDevice, oldDevAddr);
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public void ResetDeviceCache()
        {
            this.deviceCache.Reset();
        }

        public void Dispose() => this.deviceCache.Dispose();
    }

    /*public sealed class LoRaDeviceCache : IDisposable
    {
        private CancellationChangeToken resetCacheChangeToken;
        private CancellationTokenSource resetCacheToken;
        private readonly IMemoryCache memoryCache;

        public LoRaDeviceCache(IMemoryCache memoryCache)
        {
            this.resetCacheToken = new CancellationTokenSource();
            this.resetCacheChangeToken = new CancellationChangeToken(this.resetCacheToken.Token);
            this.memoryCache = memoryCache;
        }

        public LoRaDevice GetByDevEui(string devEUI)
        {
            return this.memoryCache.Get<LoRaDevice>(devEUI);
        }

        public void AddJoinDevice(LoRaDevice device)
        {
            _ = device ?? throw new ArgumentNullException(nameof(device));
            if (string.IsNullOrEmpty(device.DevEUI) || !string.IsNullOrEmpty(device.DevAddr))
            {
                throw new ArgumentException($"{nameof(device)} is not a join device");
            }

            lock (this.memoryCache)
            {
                _ = TryRemove(device.DevEUI);
                this.memoryCache.CreateEntry(device.DevEUI)
                                .SetAbsoluteExpiration(TimeSpan.FromSeconds(30))
                                .SetValue(device)
                                .RegisterPostEvictionCallback(HandleJoinDeviceEviction)
                                .ExpirationTokens.Add(this.resetCacheChangeToken);

            }

            static void HandleJoinDeviceEviction(object key, object objDevice, EvictionReason reason, object state)
            {
                if ((reason is EvictionReason.Capacity or EvictionReason.Expired) && objDevice is LoRaDevice loRaDevice)
                {
                    loRaDevice.Dispose();
                }
            }
        }

        public IEnumerable<LoRaDevice> GetByDevAddr(string devAddr)
        {
            return GetTargetDictionaryForDevAddr(devAddr).Values;
        }

        public void Add(LoRaDevice loRaDevice)
        {
            _ = loRaDevice ?? throw new ArgumentNullException(nameof(loRaDevice));

            lock (this.memoryCache)
            {
                // ensure we only cache a single instance
                _ = TryRemove(loRaDevice.DevEUI);

                var dictionary = GetTargetDictionaryForDevAddr(loRaDevice.DevAddr);
                dictionary[loRaDevice.DevEUI] = loRaDevice;

                _ = this.memoryCache.Set(loRaDevice.DevEUI, loRaDevice, this.resetCacheChangeToken);
            }

            Logger.Log(loRaDevice.DevEUI, "device added to cache", LogLevel.Debug);
        }

        public bool TryRemove(string devEUI, string oldDevAddr = null)
        {
            lock (this.memoryCache)
            {
                using var loRaDevice = GetByDevEui(devEUI);
                if (loRaDevice is { })
                {
                    this.memoryCache.Remove(devEUI);

                    if (string.IsNullOrEmpty(loRaDevice.DevAddr))
                    {
                        // join device case - a join device does not yet have a DevAddr, hence
                        // it's not registered in the DevAddr lookup table and we only have
                        // a single representation of it in the cache

                        return true;
                    }

                    LoRaDevice deviceByDevAddr = null;

                    // if no explicit devAddr was specified, we use the one from the device found
                    oldDevAddr ??= loRaDevice.DevAddr;

                    if (!this.memoryCache.TryGetValue<DevEUIToLoRaDeviceDictionary>(oldDevAddr, out var loraByDevAddr)
#pragma warning disable CA2000 // Dispose objects before losing scope: the object is properly disposed
                    || !loraByDevAddr.Remove(devEUI, out deviceByDevAddr)
#pragma warning restore CA2000 // Dispose objects before losing scope
                    || !ReferenceEquals(loRaDevice, deviceByDevAddr))
                    {
                        deviceByDevAddr?.Dispose();
                        throw new InvalidOperationException($"Cache inconsistency detected. {nameof(LoRaDevice)}.DevAddr=={loRaDevice.DevAddr} / devAddr=={oldDevAddr}");
                    }
                    return true;
                }
            }

            return false;
        }

        public void Reset()
        {
            var oldResetCacheToken = this.resetCacheToken;
            this.resetCacheToken = new CancellationTokenSource();
            this.resetCacheChangeToken = new CancellationChangeToken(this.resetCacheToken.Token);

            if (oldResetCacheToken != null && !oldResetCacheToken.IsCancellationRequested && oldResetCacheToken.Token.CanBeCanceled)
            {
                oldResetCacheToken.Cancel();
                oldResetCacheToken.Dispose();

                Logger.Log("Device cache cleared", LogLevel.Information);
            }
        }

        private DevEUIToLoRaDeviceDictionary GetTargetDictionaryForDevAddr(string devAddr, bool createIfNotExists = true)
        {
            if (createIfNotExists)
            {
                lock (this.memoryCache)
                {
                    return this.memoryCache.GetOrCreate(devAddr, (cacheEntry) =>
                    {
                        cacheEntry.SetAbsoluteExpiration(TimeSpan.FromDays(2))
                                  .RegisterPostEvictionCallback(CacheEvictionHandler)
                                  .ExpirationTokens.Add(this.resetCacheChangeToken);
                        return new DevEUIToLoRaDeviceDictionary();
                    });
                }
            }

            return this.memoryCache.Get<DevEUIToLoRaDeviceDictionary>(devAddr);
        }

        private void CacheEvictionHandler(object key, object value, EvictionReason reason, object state)
        {
            if (reason == EvictionReason.Expired && value is LoRaDevice loraDevice)
            {
                lock (this.memoryCache)
                {
                    this.memoryCache.Remove(loraDevice.DevEUI);
                    loraDevice.Dispose();
                }
            }
        }

        public void Dispose()
        {
            this.resetCacheToken.Dispose();
        }
    }//*/
}
