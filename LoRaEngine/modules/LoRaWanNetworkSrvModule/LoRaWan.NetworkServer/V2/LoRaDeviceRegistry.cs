//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools;
using LoRaTools.LoRaMessage;
using LoRaTools.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    /// <summary>
    /// LoRa device registry
    /// </summary>
    public class LoRaDeviceRegistry : ILoRaDeviceRegistry
    {
        // Caches a device making join for 2 minute
        const int INTERVAL_TO_CACHE_DEVICE_IN_JOIN_PROCESS_IN_MINUTES = 2;
        private readonly NetworkServerConfiguration configuration;
        private volatile IMemoryCache cache;
        private CancellationTokenSource resetCacheToken;
        
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceFactory deviceFactory;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;


        public LoRaDeviceRegistry(
            NetworkServerConfiguration configuration, 
            IMemoryCache cache, 
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceFactory deviceFactory)
        {
            this.configuration = configuration;
            this.resetCacheToken = new CancellationTokenSource();
            this.cache = cache;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.initializers = new HashSet<ILoRaDeviceInitializer>();
        }

        /// <summary>
        /// Registers a <see cref="ILoRaDeviceInitializer"/>
        /// </summary>
        /// <param name="initializer"></param>
        public void RegisterDeviceInitializer(ILoRaDeviceInitializer initializer) => this.initializers.Add(initializer);


        /// <summary>
        /// Gets a <see cref="DevEUIToLoRaDeviceDictionary"/> containing a list of devices given a <paramref name="devAddr"/>
        /// </summary>
        /// <remarks>
        /// This method is internal in order to allow test assembly to used it!
        /// </remarks>
        /// <param name="devAddr"></param>
        /// <returns></returns>
        internal DevEUIToLoRaDeviceDictionary InternalGetCachedDevicesForDevAddr(string devAddr)
        {
            return this.cache.GetOrCreate<DevEUIToLoRaDeviceDictionary>(devAddr, (cacheEntry) => {
                cacheEntry.SlidingExpiration = TimeSpan.FromDays(1);
                cacheEntry.ExpirationTokens.Add(new CancellationChangeToken(this.resetCacheToken.Token));
                return new DevEUIToLoRaDeviceDictionary();
            });
        }

        /// <summary>
        /// Finds a device based on the <see cref="LoRaPayloadData"/>
        /// </summary>
        /// <param name="loraPayload"></param>
        /// <returns></returns>
        public async Task<LoRaDevice> GetDeviceForPayloadAsync(LoRaPayloadData loraPayload)
        {            
            var devAddr = ConversionHelper.ByteArrayToString(loraPayload.DevAddr.ToArray());
            var devicesMatchingDevAddr = this.InternalGetCachedDevicesForDevAddr(devAddr);

            // If already in cache, return quickly
            if (devicesMatchingDevAddr.Count > 0)
            {
                var matchingDevice = devicesMatchingDevAddr.Values.FirstOrDefault(x => IsValidDeviceForPayload(x, loraPayload, logError: false));
                if (matchingDevice != null)
                {
                    if (matchingDevice.IsOurDevice)
                    {
                        Logger.Log(matchingDevice.DevEUI, "device in cache", Logger.LoggingLevel.Full);
                        return matchingDevice;
                    }
                    else
                    {
                        Logger.Log(matchingDevice.DevEUI ?? devAddr, $"device is not our device, ignore message", Logger.LoggingLevel.Info);
                        return null;
                    }
                }                
            }

            // If device was not found, search in the device API, updating local cache
            Logger.Log(devAddr, "querying the registry for device", Logger.LoggingLevel.Info);

            SearchDevicesResult searchDeviceResult = null;
            try
            {
                searchDeviceResult = await this.loRaDeviceAPIService.SearchByDevAddrAsync(devAddr);
            }
            catch (Exception ex)
            {
                Logger.Log(devAddr, $"Error searching device for payload. {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
            
            if (searchDeviceResult?.Devices != null)
            {
                foreach (var foundDevice in searchDeviceResult.Devices)
                {
                    var loRaDevice = this.deviceFactory.Create(foundDevice);
                    if (loRaDevice != null && devicesMatchingDevAddr.TryAdd(loRaDevice.DevEUI, loRaDevice))
                    {
                        try
                        {
                            // Calling initialize async here to avoid making async calls in the concurrent dictionary
                            // Since only one device will be added, we guarantee that initialization only happens once
                            if (await loRaDevice.InitializeAsync())
                            {
                                loRaDevice.IsOurDevice = string.IsNullOrEmpty(loRaDevice.GatewayID) || string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase);

                                // once added, call initializers
                                foreach (var initializer in this.initializers)
                                    initializer.Initialize(loRaDevice);

                                if (loRaDevice.DevEUI != null)
                                    Logger.Log(loRaDevice.DevEUI, "device added to cache", Logger.LoggingLevel.Full);


                                // TODO: stop if we found the matching device?
                                // If we continue we can cache for later usage, but then do it in a new thread
                                if (IsValidDeviceForPayload(loRaDevice, loraPayload, logError: false))
                                {
                                    if (!loRaDevice.IsOurDevice)
                                    {
                                        Logger.Log(loRaDevice.DevEUI ?? devAddr, $"device is not our device, ignore message", Logger.LoggingLevel.Info);
                                        return null;
                                    }
                                    
                                    return loRaDevice;
                                }
                            }
                            else
                            {
                                // could not initialize device
                                // remove it from cache since it does not have required properties
                                devicesMatchingDevAddr.TryRemove(loRaDevice.DevEUI, out _);
                            }
                        }
                        catch (Exception ex)
                        {
                            // problem initializing the device (get twin timeout, etc)
                            // remove it from the cache
                            Logger.Log(loRaDevice.DevEUI ?? devAddr, $"Error initializing device {loRaDevice.DevEUI}. {ex.Message}", Logger.LoggingLevel.Error);

                            devicesMatchingDevAddr.TryRemove(loRaDevice.DevEUI, out _);
                        }
                    }
                }
                
                // try now with updated cache
                var matchingDevice = devicesMatchingDevAddr.Values.FirstOrDefault(x => IsValidDeviceForPayload(x, loraPayload, logError: true));
                if (matchingDevice != null && !matchingDevice.IsOurDevice)
                {
                    Logger.Log(matchingDevice.DevEUI ?? devAddr, $"device is not our device, ignore message", Logger.LoggingLevel.Info);
                    return null;
                }

                return matchingDevice;
            }

            return null;
        }

        /// <summary>
        /// Checks whether a <see cref="LoRaDevice"/> is valid for a <see cref="LoRaPayloadData"/>
        /// It validates that the device has a <see cref="LoRaDevice.NwkSKey"/> and mic check
        /// </summary>
        /// <param name="loRaDevice"></param>
        /// <param name="loraPayload"></param>
        /// <param name="logError">Indicates if error should be log if mic check fails</param>
        /// <returns></returns>
        private bool IsValidDeviceForPayload(LoRaDevice loRaDevice, LoRaPayloadData loraPayload, bool logError)
        {            
            if (string.IsNullOrEmpty(loRaDevice.NwkSKey))
                return false;

            var checkMicResult = loraPayload.CheckMic(loRaDevice.NwkSKey);
            if (!checkMicResult && logError)
            {
                Logger.Log(loRaDevice.DevEUI, $"with devAddr {loRaDevice.DevAddr} check MIC failed", Logger.LoggingLevel.Full);
            }

            return checkMicResult;
        }

        // Creates cache key for join device loader: "joinloader:{devEUI}"
        string GetJoinDeviceLoaderCacheKey(string devEUI) => string.Concat("joinloader:", devEUI);

        // Removes join device loader from cache
        void RemoveJoinDeviceLoader(string devEUI) => this.cache.Remove(GetJoinDeviceLoaderCacheKey(devEUI));

        // Gets or adds a join device loader to the memory cache
        JoinDeviceLoader GetOrAddJoinDeviceLoader(IoTHubDeviceInfo ioTHubDeviceInfo)
        {
            return this.cache.GetOrCreate(GetJoinDeviceLoaderCacheKey(ioTHubDeviceInfo.DevEUI), (entry) => {
                entry.SlidingExpiration = TimeSpan.FromMinutes(INTERVAL_TO_CACHE_DEVICE_IN_JOIN_PROCESS_IN_MINUTES);
                return new JoinDeviceLoader(ioTHubDeviceInfo, this.deviceFactory);
            });
        }

        /// <summary>
        /// Searchs for devices that match the join request
        /// </summary>
        /// <param name="devEUI"></param>
        /// <param name="appEUI"></param>
        /// <param name="devNonce"></param>        
        /// <returns></returns>
        public async Task<LoRaDevice> GetDeviceForJoinRequestAsync(string devEUI, string appEUI, string devNonce)
        {
            if (string.IsNullOrEmpty(devEUI) || string.IsNullOrEmpty(appEUI) || string.IsNullOrEmpty(devNonce))
            {
                Logger.Log(devEUI, "join refused: missing devEUI/AppEUI/DevNonce in request", Logger.LoggingLevel.Error);
                return null;
            }

            Logger.Log(devEUI, "querying the registry for OTTA device", Logger.LoggingLevel.Info);

            try
            {
                var searchDeviceResult = await this.loRaDeviceAPIService.SearchAndLockForJoinAsync(
                    gatewayID: configuration.GatewayID,
                    devEUI: devEUI,
                    appEUI: appEUI,
                    devNonce: devNonce);

                if (searchDeviceResult.IsDevNonceAlreadyUsed)
                {
                    Logger.Log(devEUI, $"join refused: DevNonce already used by this device", Logger.LoggingLevel.Info);
                    return null;
                }

                if (searchDeviceResult.Devices?.Count == 0)
                {
                    Logger.Log(devEUI, "join refused: no devices found matching join request", Logger.LoggingLevel.Info);
                    return null;
                }

                var matchingDeviceInfo = searchDeviceResult.Devices[0];
                var loader = GetOrAddJoinDeviceLoader(matchingDeviceInfo);
                var loRaDevice = await loader.WaitComplete();
                if (!loader.CanCache)
                    RemoveJoinDeviceLoader(devEUI);

                return loRaDevice;
            }
            catch (Exception ex)
            {
                Logger.Log(devEUI, $"failed to get join devices from api. {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Searchs for devices that match the join request
        /// </summary>
        /// <param name="devEUI"></param>
        /// <param name="appEUI"></param>
        /// <param name="devNonce"></param>        
        /// <returns></returns>
        public async Task<LoRaDevice> GetDeviceForJoinRequestAsyncOld(string devEUI, string appEUI, string devNonce)
        {
            if (string.IsNullOrEmpty(devEUI) || string.IsNullOrEmpty(appEUI) || string.IsNullOrEmpty(devNonce))
            {
                Logger.Log(devEUI, "join refused: missing devEUI/AppEUI/DevNonce in request", Logger.LoggingLevel.Error);
                return null;
            }

            Logger.Log(devEUI, "querying the registry for OTTA device", Logger.LoggingLevel.Info);

            try
            {
                var searchDeviceResult = await this.loRaDeviceAPIService.SearchAndLockForJoinAsync(
                    gatewayID: configuration.GatewayID,
                    devEUI: devEUI,
                    appEUI: appEUI,
                    devNonce: devNonce);

                if (searchDeviceResult.IsDevNonceAlreadyUsed)
                {
                    Logger.Log(devEUI, $"join refused: DevNonce already used by this device", Logger.LoggingLevel.Info);
                    return null;
                }

                if (searchDeviceResult.Devices == null || !searchDeviceResult.Devices.Any())
                {
                    Logger.Log(devEUI, "join refused: no devices found matching join request", Logger.LoggingLevel.Info);
                    return null;
                }

                var matchingDeviceInfo = searchDeviceResult.Devices[0];
                var loRaDevice = this.deviceFactory.Create(matchingDeviceInfo);
                if (loRaDevice != null)
                {
                    try
                    {
                        Logger.Log(loRaDevice.DevEUI, $"getting twins for OTAA for device", Logger.LoggingLevel.Info);
                        if (await loRaDevice.InitializeAsync())
                        {
                            //AddToPendingJoinRequests(loRaDevice);
                            Logger.Log(loRaDevice.DevEUI, $"done getting twins for OTAA device", Logger.LoggingLevel.Info);
                        }

                    }
                    catch (Exception ex)
                    {
                        // problem initializing the device (get twin timeout, etc)
                        // remove it from the cache
                        Logger.Log(loRaDevice.DevEUI, $"join refused: error initializing OTAA device. {ex.Message}", Logger.LoggingLevel.Error);

                        loRaDevice = null;
                    }
                }

                return loRaDevice;
            }
            catch (Exception ex)
            {
                Logger.Log(devEUI, $"failed to get join devices from api. {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Updates a device after a successful login
        /// </summary>
        /// <param name="loRaDevice"></param>
        public void UpdateDeviceAfterJoin(LoRaDevice loRaDevice)
        {
            var devicesMatchingDevAddr = this.cache.GetOrCreate<DevEUIToLoRaDeviceDictionary>(loRaDevice.DevAddr, (cacheEntry) => {
                cacheEntry.SlidingExpiration = TimeSpan.FromDays(1);
                cacheEntry.ExpirationTokens.Add(new CancellationChangeToken(this.resetCacheToken.Token));
                return new DevEUIToLoRaDeviceDictionary();
            });


            // if there is an instance, overwrite it            
            devicesMatchingDevAddr.AddOrUpdate(loRaDevice.DevEUI, loRaDevice, (key, existing) => loRaDevice);

            // don't remove from pending joins because the device can try again if there was
            // a problem in the transmission
            //TryRemoveJoinDeviceLoader(loRaDevice.DevEUI);

            // once added, call initializers
            foreach (var initializer in this.initializers)
                initializer.Initialize(loRaDevice);
        }
       
        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public void ResetDeviceCache()
        {
            var oldResetCacheToken = resetCacheToken;
            resetCacheToken = new CancellationTokenSource();

            if (oldResetCacheToken != null && !oldResetCacheToken.IsCancellationRequested && oldResetCacheToken.Token.CanBeCanceled)
            {
                oldResetCacheToken.Cancel();
                oldResetCacheToken.Dispose();

                Logger.Log("device cache cleared", Logger.LoggingLevel.Info);
            }            
        }
    }
}