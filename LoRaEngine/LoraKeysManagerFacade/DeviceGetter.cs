// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

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
        /// Entry point function for getting devices.
        /// </summary>
        [FunctionName(nameof(GetDevice))]
        public async Task<IActionResult> GetDevice(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            // ABP parameters
            string devAddrString = req.Query["DevAddr"];
            // OTAA parameters
            string rawDevEui = req.Query["DevEUI"];
            string rawDevNonce = req.Query["DevNonce"];
            var gatewayId = req.Query["GatewayId"];

            DevEui? devEui = null;
            if (!string.IsNullOrEmpty(rawDevEui))
            {
                if (EuiValidator.TryParseAndValidate(rawDevEui, out var parsedDevEui))
                {
                    devEui = parsedDevEui;
                }
                else
                {
                    return new BadRequestObjectResult("Dev EUI is invalid.");
                }
            }

            try
            {
                DevNonce? devNonce = ushort.TryParse(rawDevNonce, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? new DevNonce(d) : null;
                DevAddr? devAddr = DevAddr.TryParse(devAddrString, out var someDevAddr) ? someDevAddr : null;
                var results = await GetDeviceList(devEui, gatewayId, devNonce, devAddr, log);
                var json = JsonConvert.SerializeObject(results);
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

        public async Task<List<IoTHubDeviceInfo>> GetDeviceList(DevEui? devEUI, string gatewayId, DevNonce? devNonce, DevAddr? devAddr, ILogger log = null)
        {
            var results = new List<IoTHubDeviceInfo>();

            if (devEUI is { } someDevEui)
            {
                var joinInfo = await TryGetJoinInfoAndValidateAsync(someDevEui, gatewayId, log);

                // OTAA join
                using var deviceCache = new LoRaDeviceCache(this.cacheStore, someDevEui, gatewayId);
                var cacheKeyDevNonce = string.Concat(devEUI, ":", devNonce);

                if (this.cacheStore.StringSet(cacheKeyDevNonce, devNonce?.ToString(), TimeSpan.FromMinutes(5), onlyIfNotExists: true))
                {
                    var iotHubDeviceInfo = new IoTHubDeviceInfo
                    {
                        DevEUI = someDevEui,
                        PrimaryKey = joinInfo.PrimaryKey
                    };

                    results.Add(iotHubDeviceInfo);

                    if (await deviceCache.TryToLockAsync())
                    {
                        deviceCache.ClearCache(); // clear the fcnt up/down after the join
                        log?.LogDebug("Removed key '{key}':{gwid}", someDevEui, gatewayId);
                    }
                    else
                    {
                        log?.LogWarning("Failed to acquire lock for '{key}'", someDevEui);
                    }
                }
                else
                {
                    log?.LogDebug("dev nonce already used. Ignore request '{key}':{gwid}", someDevEui, gatewayId);
                    throw new DeviceNonceUsedException();
                }
            }
            else if (devAddr is { } someDevAddr)
            {
                // ABP or normal message

                // TODO check for sql injection
                var devAddrCache = new LoRaDevAddrCache(this.cacheStore, this.registryManager, log, gatewayId);
                if (await devAddrCache.TryTakeDevAddrUpdateLock(someDevAddr))
                {
                    try
                    {
                        if (devAddrCache.TryGetInfo(someDevAddr, out var devAddressesInfo))
                        {
                            for (var i = 0; i < devAddressesInfo.Count; i++)
                            {
                                if (devAddressesInfo[i].DevEUI is { } someDevEuiPrime)
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
                                        devAddressesInfo[i].PrimaryKey = await LoadPrimaryKeyAsync(someDevEuiPrime);
                                        results.Add(devAddressesInfo[i]);
                                        _ = devAddrCache.StoreInfo(devAddressesInfo[i]);
                                    }

                                    // even if we fail to acquire the lock we wont enter in the next condition as devaddressinfo is not null
                                }
                            }
                        }

                        // if the cache results are null, we query the IoT Hub.
                        // if the device is not found is the cache we query, if there was something, it is probably not our device.
                        if (results.Count == 0 && devAddressesInfo == null)
                        {
                            var query = this.registryManager.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{someDevAddr}' OR properties.reported.DevAddr ='{someDevAddr}'", 100);
                            var resultCount = 0;
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
                                            DevAddr = someDevAddr,
                                            DevEUI = DevEui.Parse(twin.DeviceId),
                                            PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey,
                                            GatewayId = twin.GetGatewayID(),
                                            NwkSKey = twin.GetNwkSKey(),
                                            LastUpdatedTwins = twin.Properties.Desired.GetLastUpdated()
                                        };
                                        results.Add(iotHubDeviceInfo);
                                        _ = devAddrCache.StoreInfo(iotHubDeviceInfo);
                                    }

                                    resultCount++;
                                }
                            }

                            // todo save when not our devaddr
                            if (resultCount == 0)
                            {
                                _ = devAddrCache.StoreInfo(new DevAddrCacheInfo()
                                {
                                    DevAddr = someDevAddr
                                });
                            }
                        }
                    }
                    finally
                    {
                        _ = devAddrCache.ReleaseDevAddrUpdateLock(someDevAddr);
                    }
                }
            }
            else
            {
                throw new ArgumentException("Missing devEUI or devAddr");
            }

            return results;
        }

        private async Task<string> LoadPrimaryKeyAsync(DevEui devEUI)
        {
            var device = await this.registryManager.GetDeviceAsync(devEUI.AsIotHubDeviceId());
            if (device == null)
            {
                return null;
            }

            return device.Authentication.SymmetricKey?.PrimaryKey;
        }

        private async Task<JoinInfo> TryGetJoinInfoAndValidateAsync(DevEui devEUI, string gatewayId, ILogger log)
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

                        var device = await this.registryManager.GetDeviceAsync(devEUI.AsIotHubDeviceId());
                        if (device != null)
                        {
                            joinInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                            var twin = await this.registryManager.GetTwinAsync(devEUI.AsIotHubDeviceId());
                            var deviceGatewayId = twin.GetGatewayID();
                            if (!string.IsNullOrEmpty(deviceGatewayId))
                            {
                                joinInfo.DesiredGateway = deviceGatewayId;
                            }
                        }

                        _ = this.cacheStore.ObjectSet(cacheKeyJoinInfo, joinInfo, TimeSpan.FromMinutes(60));
                        log?.LogDebug("updated cache with join info '{key}':{gwid}", devEUI, gatewayId);
                    }
                }
                finally
                {
                    _ = this.cacheStore.LockRelease(lockKeyJoinInfo, gatewayId);
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
