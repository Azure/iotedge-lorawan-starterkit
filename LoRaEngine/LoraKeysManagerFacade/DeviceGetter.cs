// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaWan.Shared;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class DeviceGetter
    {
        private readonly RegistryManager registryManager;
        private readonly ILoRaDeviceCacheStore cacheStore;

        public DeviceGetter(RegistryManager registryManager, ILoRaDeviceCacheStore cacheStore)
        {
            this.registryManager = registryManager;
            this.cacheStore = cacheStore;
        }

        /// <summary>
        /// Entry point function for getting devices
        /// </summary>
        [FunctionName(nameof(GetDevice))]
        public async Task<IActionResult> GetDevice(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req,
            ILogger log)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            // ABP parameters
            string devAddr = req.Query["DevAddr"];
            // OTAA parameters
            string devEUI = req.Query["DevEUI"];
            string devNonce = req.Query["DevNonce"];
            string gatewayId = req.Query["GatewayId"];

            if (devEUI != null)
            {
                EUIValidator.ValidateDevEUI(devEUI);
            }

            try
            {
                var results = await this.GetDeviceList(devEUI, gatewayId, devNonce, devAddr, log);
                string json = JsonConvert.SerializeObject(results);
                return new OkObjectResult(json);
            }
            catch (DeviceNonceUsedException)
            {
                return new BadRequestObjectResult("UsedDevNonce");
            }
            catch (JoinRefusedException ex)
            {
                log.LogDebug("Join refused: {msg}", ex.Message);
                return new BadRequestObjectResult("JoinRefused: " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
        }

        public async Task<List<IoTHubDeviceInfo>> GetDeviceList(string devEUI, string gatewayId, string devNonce, string devAddr, ILogger log = null)
        {
            var results = new List<IoTHubDeviceInfo>();

            if (devEUI != null)
            {
                var joinInfo = await this.TryGetJoinInfoAndValidateAsync(devEUI, gatewayId, log);

                // OTAA join
                using (var deviceCache = new LoRaDeviceCache(this.cacheStore, devEUI, gatewayId))
                {
                    var cacheKeyDevNonce = string.Concat(devEUI, ":", devNonce);
                    var lockKeyDevNonce = string.Concat(cacheKeyDevNonce, ":joinlockdevnonce");

                    if (this.cacheStore.StringSet(cacheKeyDevNonce, devNonce, TimeSpan.FromMinutes(5), onlyIfNotExists: true))
                    {
                        var iotHubDeviceInfo = new IoTHubDeviceInfo
                        {
                            DevEUI = devEUI,
                            PrimaryKey = joinInfo.PrimaryKey
                        };

                        results.Add(iotHubDeviceInfo);

                        if (await deviceCache.TryToLockAsync())
                        {
                            this.cacheStore.KeyDelete(devEUI);
                            log?.LogDebug("Removed key '{key}':{gwid}", devEUI, gatewayId);
                        }
                        else
                        {
                            log?.LogWarning("Failed to acquire lock for '{key}'", devEUI);
                        }
                    }
                    else
                    {
                        log?.LogDebug("dev nonce already used. Ignore request '{key}':{gwid}", devEUI, gatewayId);
                        throw new DeviceNonceUsedException();
                    }
                }
            }
            else if (devAddr != null)
            {
                // ABP or normal message

                // TODO check for sql injection
                devAddr = devAddr.Replace('\'', ' ');
                var devAddrCache = new LoRaDevAddrCache(this.cacheStore, this.registryManager, log, gatewayId);
                if (await devAddrCache.TryTakeDevAddrUpdateLock(devAddr))
                {
                    try
                    {
                        if (devAddrCache.TryGetInfo(devAddr, out List<DevAddrCacheInfo> devAddressesInfo))
                        {
                            for (int i = 0; i < devAddressesInfo.Count; i++)
                            {
                                if (!string.IsNullOrEmpty(devAddressesInfo[i].DevEUI))
                                {
                                    // device was not yet populated
                                    if (!string.IsNullOrEmpty(devAddressesInfo[i].PrimaryKey))
                                    {
                                        results.Add(devAddressesInfo[i]);
                                    }
                                    else
                                    {
                                        // we need to load the primaryKey from IoTHub
                                        // Add a lock loadPrimaryKey get lock get
                                        devAddressesInfo[i].PrimaryKey = await this.LoadPrimaryKeyAsync(devAddressesInfo[i].DevEUI);
                                        results.Add(devAddressesInfo[i]);
                                        devAddrCache.StoreInfo(devAddressesInfo[i]);
                                    }

                                    // even if we fail to acquire the lock we wont enter in the next condition as devaddressinfo is not null
                                }
                            }
                        }

                        // if the cache results are null, we query the IoT Hub.
                        // if the device is not found is the cache we query, if there was something, it is probably not our device.
                        if (results.Count == 0 && devAddressesInfo == null)
                        {
                            if (await devAddrCache.TryTakeDevAddrUpdateLock(devAddr))
                            {
                                try
                                {
                                    var query = this.registryManager.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{devAddr}' OR properties.reported.DevAddr ='{devAddr}'", 100);
                                    int resultCount = 0;
                                    while (query.HasMoreResults)
                                    {
                                        var page = await query.GetNextAsTwinAsync();

                                        foreach (var twin in page)
                                        {
                                            if (twin.DeviceId != null)
                                            {
                                                var device = await this.registryManager.GetDeviceAsync(twin.DeviceId);
                                                var iotHubDeviceInfo = new DevAddrCacheInfo
                                                {
                                                    DevAddr = devAddr,
                                                    DevEUI = twin.DeviceId,
                                                    PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey,
                                                    GatewayId = twin.Properties.Desired.Contains("GatewayId") ?
                                                        twin.Properties.Desired["GatewayId"] as string :
                                                        string.Empty,
                                                    LastUpdatedTwins = twin.Properties.Desired.GetLastUpdated()
                                                };
                                                results.Add(iotHubDeviceInfo);
                                                devAddrCache.StoreInfo((DevAddrCacheInfo)iotHubDeviceInfo);
                                            }

                                            resultCount++;
                                        }
                                    }

                                    // todo save when not our devaddr
                                    if (resultCount == 0)
                                    {
                                        devAddrCache.StoreInfo(new DevAddrCacheInfo()
                                        {
                                            DevAddr = devAddr,
                                            DevEUI = string.Empty
                                        });
                                    }
                                }
                                finally
                                {
                                    devAddrCache.ReleaseDevAddrUpdateLock(devAddr);
                                }
                            }
                        }
                    }
                    finally
                    {
                        devAddrCache.ReleaseDevAddrUpdateLock(devAddr);
                    }
                }
            }
            else
            {
                throw new Exception("Missing devEUI or devAddr");
            }

            return results;
        }

        private async Task<string> LoadPrimaryKeyAsync(string devEUI)
        {
            var device = await this.registryManager.GetDeviceAsync(devEUI);
            if (device == null)
            {
                return null;
            }

            return device.Authentication.SymmetricKey?.PrimaryKey;
        }

        private async Task<JoinInfo> TryGetJoinInfoAndValidateAsync(string devEUI, string gatewayId, ILogger log)
        {
            var cacheKeyJoinInfo = string.Concat(devEUI, ":joininfo");
            var lockKeyJoinInfo = string.Concat(devEUI, ":joinlockjoininfo");
            JoinInfo joinInfo = null;

            if (await this.cacheStore.LockTakeAsync(lockKeyJoinInfo, gatewayId, TimeSpan.FromMinutes(5)))
            {
                try
                {
                    joinInfo = this.cacheStore.GetObject<JoinInfo>(cacheKeyJoinInfo);
                    if (joinInfo == null)
                    {
                        joinInfo = new JoinInfo();

                        var device = await this.registryManager.GetDeviceAsync(devEUI);
                        if (device != null)
                        {
                            joinInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                            var twin = await this.registryManager.GetTwinAsync(devEUI);
                            const string GatewayIdProperty = "GatewayID";
                            if (twin.Properties.Desired.Contains(GatewayIdProperty))
                            {
                                joinInfo.DesiredGateway = twin.Properties.Desired[GatewayIdProperty].Value as string;
                            }
                        }

                        this.cacheStore.ObjectSet(cacheKeyJoinInfo, joinInfo, TimeSpan.FromMinutes(60));
                        log?.LogDebug("updated cache with join info '{key}':{gwid}", devEUI, gatewayId);
                    }
                }
                finally
                {
                    this.cacheStore.LockRelease(lockKeyJoinInfo, gatewayId);
                }

                if (string.IsNullOrEmpty(joinInfo.PrimaryKey))
                {
                    throw new JoinRefusedException("Not in our network.");
                }

                if (!string.IsNullOrEmpty(joinInfo.DesiredGateway) &&
                    !joinInfo.DesiredGateway.Equals(gatewayId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new JoinRefusedException($"Not the owning gateway. Owning gateway is '{joinInfo.DesiredGateway}'");
                }

                log?.LogDebug("got LogInfo '{key}':{gwid} attached gw: {desiredgw}", devEUI, gatewayId, joinInfo.DesiredGateway);
            }
            else
            {
                throw new JoinRefusedException("Failed to acquire lock for joininfo");
            }

            return joinInfo;
        }
    }
}
