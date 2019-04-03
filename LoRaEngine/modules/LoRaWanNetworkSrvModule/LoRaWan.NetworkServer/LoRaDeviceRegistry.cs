// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// LoRa device registry
    /// </summary>
    public class LoRaDeviceRegistry : ILoRaDeviceRegistry
    {
        // Caches a device making join for 30 minutes
        const int INTERVAL_TO_CACHE_DEVICE_IN_JOIN_PROCESS_IN_MINUTES = 30;

        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceFactory deviceFactory;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;
        private readonly NetworkServerConfiguration configuration;
        readonly object getOrCreateLoadingDevicesRequestQueueLock;
        readonly object getOrCreateJoinDeviceLoaderLock;

        readonly object devEUIToLoRaDeviceDictionaryLock;

        private volatile IMemoryCache cache;

        private CancellationChangeToken resetCacheChangeToken;
        private CancellationTokenSource resetCacheToken;

        /// <summary>
        /// Gets or sets the interval in which devices will be loaded
        /// Only affect reload attempts for same DevAddr
        /// </summary>
        public TimeSpan DevAddrReloadInterval { get; set; }

        public LoRaDeviceRegistry(
            NetworkServerConfiguration configuration,
            IMemoryCache cache,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceFactory deviceFactory)
        {
            this.configuration = configuration;
            this.resetCacheToken = new CancellationTokenSource();
            this.resetCacheChangeToken = new CancellationChangeToken(this.resetCacheToken.Token);
            this.cache = cache;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.initializers = new HashSet<ILoRaDeviceInitializer>();
            this.DevAddrReloadInterval = TimeSpan.FromSeconds(30);
            this.getOrCreateLoadingDevicesRequestQueueLock = new object();
            this.getOrCreateJoinDeviceLoaderLock = new object();
            this.devEUIToLoRaDeviceDictionaryLock = new object();
        }

        /// <summary>
        /// Registers a <see cref="ILoRaDeviceInitializer"/>
        /// </summary>
        public void RegisterDeviceInitializer(ILoRaDeviceInitializer initializer) => this.initializers.Add(initializer);

        /// <summary>
        /// Gets a <see cref="DevEUIToLoRaDeviceDictionary"/> containing a list of devices given a <paramref name="devAddr"/>
        /// </summary>
        /// <remarks>
        /// This method is internal in order to allow test assembly to used it!
        /// </remarks>
        internal DevEUIToLoRaDeviceDictionary InternalGetCachedDevicesForDevAddr(string devAddr, bool createIfNotExists = true)
        {
            if (createIfNotExists)
            {
                // Need to get and ensure it has started since the GetOrAdd can create multiple objects
                // https://github.com/aspnet/Extensions/issues/708
                lock (this.devEUIToLoRaDeviceDictionaryLock)
                {
                    return this.cache.GetOrCreate<DevEUIToLoRaDeviceDictionary>(devAddr, (cacheEntry) =>
                    {
                        cacheEntry.SetAbsoluteExpiration(TimeSpan.FromDays(2));
                        cacheEntry.ExpirationTokens.Add(this.resetCacheChangeToken);
                        return new DevEUIToLoRaDeviceDictionary();
                    });
                }
            }

            this.cache.TryGetValue<DevEUIToLoRaDeviceDictionary>(devAddr, out var cachedValue);
            return cachedValue;
        }

        DeviceLoaderSynchronizer GetOrCreateLoadingDevicesRequestQueue(string devAddr)
        {
            // Need to get and ensure it has started since the GetOrAdd can create multiple objects
            // https://github.com/aspnet/Extensions/issues/708
            lock (this.getOrCreateLoadingDevicesRequestQueueLock)
            {
                return this.cache.GetOrCreate<DeviceLoaderSynchronizer>(
                    $"devaddrloader:{devAddr}",
                    (ce) =>
                    {
                        var cts = new CancellationTokenSource();
                        ce.ExpirationTokens.Add(new CancellationChangeToken(cts.Token));

                        var destinationDictionary = this.InternalGetCachedDevicesForDevAddr(devAddr);
                        var originalDeviceCount = destinationDictionary.Count;
                        var loader = new DeviceLoaderSynchronizer(
                            devAddr,
                            this.loRaDeviceAPIService,
                            this.deviceFactory,
                            destinationDictionary,
                            this.initializers,
                            this.configuration,
                            (t, l) =>
                            {
                                // If the operation to load was successfull
                                // wait for 30 seconds for pending requests to go through and avoid additional calls
                                if (t.IsCompletedSuccessfully && !l.HasLoadingDeviceError && this.DevAddrReloadInterval > TimeSpan.Zero)
                                {
                                    // remove from cache after 30 seconds
                                    cts.CancelAfter(this.DevAddrReloadInterval);
                                }
                                else
                                {
                                    // remove from cache now
                                    cts.Cancel();
                                }
                            },
                            (l) => this.UpdateDeviceRegistration(l));

                        return loader;
                    });
            }
        }

        public ILoRaDeviceRequestQueue GetLoRaRequestQueue(LoRaRequest request)
        {
            var devAddr = ConversionHelper.ByteArrayToString(request.Payload.DevAddr);
            var devicesMatchingDevAddr = this.InternalGetCachedDevicesForDevAddr(devAddr);

            // If already in cache, return quickly
            if (devicesMatchingDevAddr.Count > 0)
            {
                var cachedMatchingDevice = devicesMatchingDevAddr.Values.FirstOrDefault(x => this.IsValidDeviceForPayload(x, (LoRaPayloadData)request.Payload, logError: false));
                if (cachedMatchingDevice != null)
                {
                    Logger.Log(cachedMatchingDevice.DevEUI, "device in cache", LogLevel.Debug);
                    if (cachedMatchingDevice.IsOurDevice)
                    {
                        return cachedMatchingDevice;
                    }

                    return new ExternalGatewayLoRaRequestQueue(cachedMatchingDevice);
                }
            }

            // not in cache, need to make a single search by dev addr
            return this.GetOrCreateLoadingDevicesRequestQueue(devAddr);
        }

        /// <summary>
        /// Called to sync up list of devices kept by device registry
        /// </summary>
        void UpdateDeviceRegistration(LoRaDevice loRaDevice, string oldDevAddr = null)
        {
            var dictionary = this.InternalGetCachedDevicesForDevAddr(loRaDevice.DevAddr);
            dictionary.AddOrUpdate(loRaDevice.DevEUI, loRaDevice, (k, old) =>
            {
                return loRaDevice;
            });

            this.cache.Set(CacheKeyForDevEUIDevice(loRaDevice.DevEUI), loRaDevice, this.resetCacheChangeToken);
            Logger.Log(loRaDevice.DevEUI, "device added to cache", LogLevel.Debug);

            if (!string.IsNullOrEmpty(oldDevAddr))
            {
                var oldDevAddrDictionary = this.InternalGetCachedDevicesForDevAddr(oldDevAddr, createIfNotExists: false);
                if (oldDevAddrDictionary != null && oldDevAddrDictionary.TryRemove(loRaDevice.DevEUI, out var existingDevice))
                {
                    Logger.Log(loRaDevice.DevEUI, $"previous device devAddr ({oldDevAddr}) removed from cache.", LogLevel.Debug);
                }
            }
        }

        internal static string CacheKeyForDevEUIDevice(string devEUI) => string.Concat("deveui:", devEUI);

        /// <summary>
        /// Gets a device by DevEUI
        /// </summary>
        public async Task<LoRaDevice> GetDeviceByDevEUIAsync(string devEUI)
        {
            if (this.cache.TryGetValue<LoRaDevice>(CacheKeyForDevEUIDevice(devEUI), out var cachedDevice))
                return cachedDevice;

            var deviceInfo = await this.loRaDeviceAPIService.SearchByDevEUIAsync(devEUI);
            if (deviceInfo?.Devices == null || deviceInfo.Devices.Count == 0)
                return null;

            var loRaDevice = this.deviceFactory.Create(deviceInfo.Devices[0]);
            await loRaDevice.InitializeAsync();
            if (this.initializers != null)
            {
                foreach (var initializers in this.initializers)
                {
                    initializers.Initialize(loRaDevice);
                }
            }

            this.UpdateDeviceRegistration(loRaDevice);

            return loRaDevice;
        }

        /// <summary>
        /// Checks whether a <see cref="LoRaDevice"/> is valid for a <see cref="LoRaPayloadData"/>
        /// It validates that the device has a <see cref="LoRaDevice.NwkSKey"/> and mic check
        /// </summary>
        /// <param name="logError">Indicates if error should be log if mic check fails</param>
        private bool IsValidDeviceForPayload(LoRaDevice loRaDevice, LoRaPayloadData loraPayload, bool logError)
        {
            if (string.IsNullOrEmpty(loRaDevice.NwkSKey))
                return false;

            var checkMicResult = loRaDevice.ValidateMic(loraPayload);
            if (!checkMicResult && logError)
            {
                Logger.Log(loRaDevice.DevEUI, $"with devAddr {loRaDevice.DevAddr} check MIC failed", LogLevel.Debug);
            }

            return checkMicResult;
        }

        // Creates cache key for join device loader: "joinloader:{devEUI}"
        string GetJoinDeviceLoaderCacheKey(string devEUI) => string.Concat("joinloader:", devEUI);

        // Removes join device loader from cache
        void RemoveJoinDeviceLoader(string devEUI) => this.cache.Remove(this.GetJoinDeviceLoaderCacheKey(devEUI));

        // Gets or adds a join device loader to the memory cache
        JoinDeviceLoader GetOrCreateJoinDeviceLoader(IoTHubDeviceInfo ioTHubDeviceInfo)
        {
            // Need to get and ensure it has started since the GetOrAdd can create multiple objects
            // https://github.com/aspnet/Extensions/issues/708
            lock (this.getOrCreateJoinDeviceLoaderLock)
            {
                return this.cache.GetOrCreate(this.GetJoinDeviceLoaderCacheKey(ioTHubDeviceInfo.DevEUI), (entry) =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(INTERVAL_TO_CACHE_DEVICE_IN_JOIN_PROCESS_IN_MINUTES);
                    return new JoinDeviceLoader(ioTHubDeviceInfo, this.deviceFactory);
                });
            }
        }

        /// <summary>
        /// Searchs for devices that match the join request
        /// </summary>
        public async Task<LoRaDevice> GetDeviceForJoinRequestAsync(string devEUI, string appEUI, string devNonce)
        {
            if (string.IsNullOrEmpty(devEUI) || string.IsNullOrEmpty(appEUI) || string.IsNullOrEmpty(devNonce))
            {
                Logger.Log(devEUI, "join refused: missing devEUI/AppEUI/DevNonce in request", LogLevel.Error);
                return null;
            }

            Logger.Log(devEUI, "querying the registry for OTAA device", LogLevel.Debug);

            try
            {
                var searchDeviceResult = await this.loRaDeviceAPIService.SearchAndLockForJoinAsync(
                    gatewayID: this.configuration.GatewayID,
                    devEUI: devEUI,
                    appEUI: appEUI,
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
                var loader = this.GetOrCreateJoinDeviceLoader(matchingDeviceInfo);
                var loRaDevice = await loader.WaitCompleteAsync();
                if (!loader.CanCache)
                    this.RemoveJoinDeviceLoader(devEUI);

                return loRaDevice;
            }
            catch (Exception ex)
            {
                Logger.Log(devEUI, $"failed to get join devices from api. {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Updates a device after a successful login
        /// </summary>
        public void UpdateDeviceAfterJoin(LoRaDevice loRaDevice, string oldDevAddr)
        {
            // once added, call initializers
            foreach (var initializer in this.initializers)
                initializer.Initialize(loRaDevice);

            this.UpdateDeviceRegistration(loRaDevice, oldDevAddr);
        }

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public void ResetDeviceCache()
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
    }
}