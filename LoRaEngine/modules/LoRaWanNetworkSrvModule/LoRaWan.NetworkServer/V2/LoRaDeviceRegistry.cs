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
                        Logger.Log(matchingDevice.DevEUI, "device in cache", Logger.LoggingLevel.Info);
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
            var searchDeviceResult = await this.loRaDeviceAPIService.SearchDevicesAsync(devAddr: devAddr);
            if (searchDeviceResult.Devices != null)
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
                                    Logger.Log(loRaDevice.DevEUI, "device added to cache", Logger.LoggingLevel.Info);


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


        /// <summary>
        /// Signs devices using OTAA authentication
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

            if (this.TryGetFromPendingJoinRequests(devEUI, out var cachedDevice))
            {
                Logger.Log(devEUI, "using device from a previous failed join attempt", Logger.LoggingLevel.Full);
                return cachedDevice;
            }

            Logger.Log(devEUI, "querying the registry for device key", Logger.LoggingLevel.Info);

            var searchDeviceResult = await this.loRaDeviceAPIService.SearchDevicesAsync(
                gatewayId: configuration.GatewayID,
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
                    await loRaDevice.InitializeAsync();
                    Logger.Log(loRaDevice.DevEUI, $"done getting twins for OTAA device", Logger.LoggingLevel.Info);

                    AddToPendingJoinRequests(loRaDevice);
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

        string GetPendingJoinRequestCacheKey(string devEUI) => string.Concat("pending_join:", devEUI);

        /// <summary>
        /// Adds <paramref name="loRaDevice"/> to in-memory list of device that are trying to join
        /// This can save a get device twin call
        /// </summary>
        /// <param name="loRaDevice"></param>
        private void AddToPendingJoinRequests(LoRaDevice loRaDevice) => this.cache.Set(GetPendingJoinRequestCacheKey(loRaDevice.DevEUI), loRaDevice, TimeSpan.FromMinutes(3));

        /// <summary>
        /// Removes a device from list of devices that are trying to login
        /// </summary>
        /// <param name="loRaDevice"></param>
        private void RemoveFromPendingJoinRequests(LoRaDevice loRaDevice) => this.cache.Remove(GetPendingJoinRequestCacheKey(loRaDevice.DevEUI));

        /// <summary>
        /// Tries to get a device from the list of pending login devices
        /// </summary>
        /// <param name="devEUI"></param>
        /// <param name="loRaDevice"></param>
        /// <returns></returns>
        bool TryGetFromPendingJoinRequests(string devEUI, out LoRaDevice loRaDevice) => this.cache.TryGetValue<LoRaDevice>(GetPendingJoinRequestCacheKey(devEUI), out loRaDevice);
        
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

            // once added, call initializers
            foreach (var initializer in this.initializers)
                initializer.Initialize(loRaDevice);

            RemoveFromPendingJoinRequests(loRaDevice);
        }

        

        /// <summary>
        /// <inheritdoc />
        /// </summary>
        public void ResetDeviceCache()
        {
            if (resetCacheToken != null && !resetCacheToken.IsCancellationRequested && resetCacheToken.Token.CanBeCanceled)
            {
                resetCacheToken.Cancel();
                resetCacheToken.Dispose();
            }

            resetCacheToken = new CancellationTokenSource();
        }
    }
}