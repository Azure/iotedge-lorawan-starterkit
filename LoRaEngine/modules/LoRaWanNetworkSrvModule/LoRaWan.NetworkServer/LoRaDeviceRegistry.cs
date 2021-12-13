// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<LoRaDeviceRegistry> logger;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;
        private readonly NetworkServerConfiguration configuration;
        private readonly object getOrCreateLoadingDevicesRequestQueueLock;
        private readonly object getOrCreateJoinDeviceLoaderLock;
        private readonly Meter meter;
        private readonly Counter<int> deviceCacheHits;
        private readonly Counter<int> deviceLoadRequests;
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
            LoRaDeviceCache deviceCache,
            ILoggerFactory loggerFactory,
            ILogger<LoRaDeviceRegistry> logger,
            Meter meter)
        {
            this.configuration = configuration;
            this.cache = cache;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.loggerFactory = loggerFactory;
            this.logger = logger;
            this.initializers = new HashSet<ILoRaDeviceInitializer>();
            DevAddrReloadInterval = TimeSpan.FromSeconds(30);
            this.getOrCreateLoadingDevicesRequestQueueLock = new object();
            this.getOrCreateJoinDeviceLoaderLock = new object();
            this.meter = meter;
            this.deviceCacheHits = meter?.CreateCounter<int>(MetricRegistry.DeviceCacheHits);
            this.deviceLoadRequests = meter?.CreateCounter<int>(MetricRegistry.DeviceLoadRequests);
            this.deviceCache = deviceCache;
        }

        /// <summary>
        /// Constructor should be used for test code only.
        /// </summary>
        internal LoRaDeviceRegistry(NetworkServerConfiguration configuration,
                                    IMemoryCache cache,
                                    LoRaDeviceAPIServiceBase loRaDeviceAPIService,
                                    ILoRaDeviceFactory deviceFactory, LoRaDeviceCache deviceCache)
            : this(configuration, cache, loRaDeviceAPIService, deviceFactory, deviceCache,
                   NullLoggerFactory.Instance, NullLogger<LoRaDeviceRegistry>.Instance, null)
        { }

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
                    GetDevLoaderCacheKey(devAddr),
                    (ce) =>
                    {
                        var cts = new CancellationTokenSource();
                        ce.ExpirationTokens.Add(new CancellationChangeToken(cts.Token));

                        var loader = new DeviceLoaderSynchronizer(
                                                    devAddr,
                                                    this.loRaDeviceAPIService,
                                                    this.deviceFactory,
                                                    this.configuration,
                                                    this.deviceCache,
                                                    this.initializers,
                                                    this.loggerFactory.CreateLogger<DeviceLoaderSynchronizer>());

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

            this.deviceLoadRequests?.Add(1);

            var devAddr = ConversionHelper.ByteArrayToString(request.Payload.DevAddr);

            if (this.cache.TryGetValue<DeviceLoaderSynchronizer>(GetDevLoaderCacheKey(devAddr), out var deviceLoader))
            {
                return deviceLoader;
            }

            if (this.deviceCache.TryGetForPayload(request.Payload, out var cachedDevice))
            {
                this.logger.LogDebug("device in cache");
                this.deviceCacheHits?.Add(1);
                if (cachedDevice.IsOurDevice)
                {
                    return cachedDevice;
                }

                return new ExternalGatewayLoRaRequestQueue(cachedDevice, this.loggerFactory.CreateLogger<ExternalGatewayLoRaRequestQueue>());
            }

            // not in cache, need to make a single search by dev addr
            return GetOrCreateLoadingDevicesRequestQueue(devAddr);
        }

        /// <summary>
        /// Gets a device by DevEUI.
        /// </summary>
        public async Task<LoRaDevice> GetDeviceByDevEUIAsync(string devEUI)
        {
            this.deviceLoadRequests?.Add(1);

            if (this.deviceCache.TryGetByDevEui(devEUI, out var cachedDevice))
            {
                this.deviceCacheHits?.Add(1);
                return cachedDevice;
            }

            var searchResult = await this.loRaDeviceAPIService.SearchByEuiAsync(DevEui.Parse(devEUI));
            if (searchResult == null || searchResult.Count == 0)
                return null;

            var firstDevice = searchResult[0];
            var loRaDevice = await this.deviceFactory.CreateAndRegisterAsync(firstDevice, CancellationToken.None);

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
        private static string GetDevLoaderCacheKey(string devAddr) => string.Concat("devaddrloader:", devAddr);

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
                    return new JoinDeviceLoader(ioTHubDeviceInfo, this.deviceFactory, this.deviceCache, this.loggerFactory.CreateLogger<JoinDeviceLoader>());
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
                this.logger.LogError("join refused: missing devEUI/AppEUI/DevNonce in request");
                return null;
            }

            this.logger.LogDebug("querying the registry for OTAA device");

            var searchDeviceResult = await this.loRaDeviceAPIService.SearchAndLockForJoinAsync(
                gatewayID: this.configuration.GatewayID,
                devEUI: devEUI,
                devNonce: devNonce);

            if (searchDeviceResult.IsDevNonceAlreadyUsed)
            {
                this.logger.LogInformation("join refused: Join already processed by another gateway.");
                return null;
            }

            if (searchDeviceResult?.Devices == null || searchDeviceResult.Devices.Count == 0)
            {
                this.logger.LogInformation(searchDeviceResult.RefusedMessage ?? "join refused: no devices found matching join request");
                return null;
            }

            var matchingDeviceInfo = searchDeviceResult.Devices[0];

            if (deviceCache.TryGetByDevEui(matchingDeviceInfo.DevEUI, out var cachedDevice))
            {
                // if we already have the device in the cache, then it is either from a previous
                // join rquest or it's a re-join. Both scenarios are ok, and we can use the cached
                // information.
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
}
