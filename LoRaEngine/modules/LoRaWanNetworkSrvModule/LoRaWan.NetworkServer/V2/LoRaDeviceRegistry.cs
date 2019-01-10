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
        // Dictionary of ILoRaDevices where key is DevEUI
        public class DevEUIDeviceDictionary : ConcurrentDictionary<string, LoRaDevice>
        {

        }

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

        public async Task<LoRaDevice> GetDeviceForPayloadAsync(LoRaPayloadData loraPayload)
        {            
            var devAddr = ConversionHelper.ByteArrayToString(loraPayload.DevAddr.ToArray());
            var devicesMatchingDevAddr = this.cache.GetOrCreate<DevEUIDeviceDictionary>(devAddr, (cacheEntry) => {
                cacheEntry.SlidingExpiration = TimeSpan.FromDays(1);
                cacheEntry.ExpirationTokens.Add(new CancellationChangeToken(this.resetCacheToken.Token));
                return new DevEUIDeviceDictionary();
            });

            // If already in cache, return quickly
            if (devicesMatchingDevAddr.Count > 0)
            {
                var matchingDevice = devicesMatchingDevAddr.Values.FirstOrDefault(x => IsValidDeviceForPayload(x, loraPayload));
                if (matchingDevice != null)
                {
                    if (matchingDevice.IsOurDevice)
                    {
                        Logger.Log(devAddr, "device in cache", Logger.LoggingLevel.Info);
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
                    if (devicesMatchingDevAddr.TryAdd(foundDevice.DevEUI, loRaDevice))
                    {
                        // Calling initialize async here to avoid making async calls in the concurrent dictionary
                        await loRaDevice.InitializeAsync();
                        loRaDevice.IsOurDevice = string.IsNullOrEmpty(loRaDevice.GatewayID) || string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase);

                        // once added, call initializers
                        foreach (var initializer in this.initializers)
                            initializer.Initialize(loRaDevice);

                        if (loRaDevice.DevEUI != null)
                            Logger.Log(loRaDevice.DevEUI, "device added to cache", Logger.LoggingLevel.Info);


                        // TODO: stop if we found the matching device?
                        // If we continue we can cache for later usage, but then do it in a new thread
                        if (IsValidDeviceForPayload(loRaDevice, loraPayload))
                        {
                            if (!loRaDevice.IsOurDevice)
                            {
                                Logger.Log(loRaDevice.DevEUI ?? devAddr, $"device is not our device, ignore message", Logger.LoggingLevel.Info);
                                return null;
                            }
                            
                            return loRaDevice;
                        }
                    }
                }
                
                // try now with updated cache
                var matchingDevice = devicesMatchingDevAddr.Values.FirstOrDefault(x => IsValidDeviceForPayload(x, loraPayload));
                if (matchingDevice != null && !matchingDevice.IsOurDevice)
                {
                    Logger.Log(matchingDevice.DevEUI ?? devAddr, $"device is not our device, ignore message", Logger.LoggingLevel.Info);
                    return null;
                }

                return matchingDevice;
            }

            return null;
        }

        private bool IsValidDeviceForPayload(LoRaDevice loRaDevice, LoRaPayloadData loraPayload)
        {            
            if (string.IsNullOrEmpty(loRaDevice.NwkSKey))
                return false;

            // TODO: check with Mikhail why tests are failing here
            return loraPayload.CheckMic(loRaDevice.NwkSKey);
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
                string errorMsg = "Missing devEUI/AppEUI/DevNonce in the OTAARequest";
                Logger.Log(devEUI, errorMsg, Logger.LoggingLevel.Error);
                return null;
            }

            Logger.Log(devEUI, $"querying the registry for device key", Logger.LoggingLevel.Info);

            var searchDeviceResult = await this.loRaDeviceAPIService.SearchDevicesAsync(
                gatewayId: configuration.GatewayID,
                devEUI: devEUI,
                appEUI: appEUI,
                devNonce: devNonce);

            if (searchDeviceResult.IsDevNonceAlreadyUsed)
            {
                Logger.Log(devEUI, $"DevNonce already used by this device", Logger.LoggingLevel.Info);
                return null;
            }

            if (searchDeviceResult.Devices == null || !searchDeviceResult.Devices.Any())
                return null;


            var matchingDeviceInfo = searchDeviceResult.Devices[0];
            var loRaDevice = this.deviceFactory.Create(matchingDeviceInfo);
            if (loRaDevice != null)
            {
                Logger.Log(loRaDevice.DevEUI, $"getting twins for OTAA for device", Logger.LoggingLevel.Info);
                await loRaDevice.InitializeAsync();
                Logger.Log(loRaDevice.DevEUI, $"done getting twins for OTAA device", Logger.LoggingLevel.Info);
            }


            return loRaDevice;
        }

        public void UpdateDeviceAfterJoin(LoRaDevice loRaDevice)
        {
            var devicesMatchingDevAddr = this.cache.GetOrCreate<DevEUIDeviceDictionary>(loRaDevice.DevAddr, (cacheEntry) => {
                cacheEntry.SlidingExpiration = TimeSpan.FromDays(1);
                cacheEntry.ExpirationTokens.Add(new CancellationChangeToken(this.resetCacheToken.Token));
                return new DevEUIDeviceDictionary();
            });


            // if there is an instance, overwrite it            
            devicesMatchingDevAddr.AddOrUpdate(loRaDevice.DevEUI, loRaDevice, (key, existing) => loRaDevice);

            // once added, call initializers
            foreach (var initializer in this.initializers)
                initializer.Initialize(loRaDevice);
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