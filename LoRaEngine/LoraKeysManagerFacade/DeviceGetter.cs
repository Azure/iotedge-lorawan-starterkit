// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DeviceGetter
    {
        private readonly IDeviceRegistryManager registryManager;
        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly ILogger<DeviceGetter> logger;

        public DeviceGetter(IDeviceRegistryManager registryManager, ILoRaDeviceCacheStore cacheStore, ILogger<DeviceGetter> logger)
        {
            this.registryManager = registryManager;
            this.cacheStore = cacheStore;
            this.logger = logger;
        }

        /// <summary>
        /// Entry point function for getting devices.
        /// </summary>
        [FunctionName(nameof(GetDevice))]
        public async Task<IActionResult> GetDevice(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
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
                if (DevEui.TryParse(rawDevEui, EuiParseOptions.ForbidInvalid, out var parsedDevEui))
                {
                    devEui = parsedDevEui;
                }
                else
                {
                    return new BadRequestObjectResult("Dev EUI is invalid.");
                }
            }

            using var deviceScope = this.logger.BeginDeviceScope(devEui);

            try
            {
                DevNonce? devNonce = ushort.TryParse(rawDevNonce, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? new DevNonce(d) : null;
                DevAddr? devAddr = DevAddr.TryParse(devAddrString, out var someDevAddr) ? someDevAddr : null;

                using var devAddrScope = this.logger.BeginDeviceAddressScope(devAddr);

                var results = await GetDeviceList(devEui, gatewayId, devNonce, devAddr);
                var json = JsonConvert.SerializeObject(results);
                return new OkObjectResult(json);
            }
            catch (DeviceNonceUsedException)
            {
                return new BadRequestObjectResult("UsedDevNonce");
            }
            catch (JoinRefusedException ex) when (ExceptionFilterUtility.True(() => this.logger.LogDebug("Join refused: {msg}", ex.Message)))
            {
                return new BadRequestObjectResult("JoinRefused: " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
        }

        public async Task<List<IoTHubDeviceInfo>> GetDeviceList(DevEui? devEUI, string gatewayId, DevNonce? devNonce, DevAddr? devAddr)
        {
            var results = new List<IoTHubDeviceInfo>();

            if (devEUI is { } someDevEui)
            {
                var joinInfo = await TryGetJoinInfoAndValidateAsync(someDevEui, gatewayId);

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
                        this.logger.LogDebug("Removed key '{key}':{gwid}", someDevEui, gatewayId);
                    }
                    else
                    {
                        this.logger.LogWarning("Failed to acquire lock for '{key}'", someDevEui);
                    }
                }
                else
                {
                    this.logger.LogDebug("dev nonce already used. Ignore request '{key}':{gwid}", someDevEui, gatewayId);
                    throw new DeviceNonceUsedException();
                }
            }
            else if (devAddr is { } someDevAddr)
            {
                // ABP or normal message

                // TODO check for sql injection
                var devAddrCache = new LoRaDevAddrCache(this.cacheStore, this.registryManager, this.logger, gatewayId);
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
                            var query = this.registryManager.FindLoRaDeviceByDevAddr(someDevAddr);
                            var resultCount = 0;
                            while (query.HasMoreResults)
                            {
                                var page = await query.GetNextPageAsync();

                                foreach (var twin in page)
                                {
                                    if (twin.DeviceId != null)
                                    {
                                        var iotHubDeviceInfo = new DevAddrCacheInfo
                                        {
                                            DevAddr = someDevAddr,
                                            DevEUI = DevEui.Parse(twin.DeviceId),
                                            PrimaryKey = await this.registryManager.GetDevicePrimaryKeyAsync(twin.DeviceId),
                                            GatewayId = twin.GetGatewayID(),
                                            NwkSKey = twin.GetNwkSKey(),
                                            LastUpdatedTwins = twin.Properties.Desired.GetLastUpdated()
                                        };
                                        results.Add(iotHubDeviceInfo);
                                        devAddrCache.StoreInfo(iotHubDeviceInfo);
                                    }

                                    resultCount++;
                                }
                            }

                            // todo save when not our devaddr
                            if (resultCount == 0)
                            {
                                devAddrCache.StoreInfo(new DevAddrCacheInfo()
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
            return await this.registryManager.GetDevicePrimaryKeyAsync(devEUI.ToString());
        }

        private async Task<JoinInfo> TryGetJoinInfoAndValidateAsync(DevEui devEUI, string gatewayId)
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
                        joinInfo = new JoinInfo
                        {
                            PrimaryKey = await this.registryManager.GetDevicePrimaryKeyAsync(devEUI.ToString())
                        };

                        var twin = await this.registryManager.GetLoRaDeviceTwinAsync(devEUI.ToString());
                        var deviceGatewayId = twin.GetGatewayID();
                        if (!string.IsNullOrEmpty(deviceGatewayId))
                        {
                            joinInfo.DesiredGateway = deviceGatewayId;
                        }

                        _ = this.cacheStore.ObjectSet(cacheKeyJoinInfo, joinInfo, TimeSpan.FromMinutes(60));
                        this.logger.LogDebug("updated cache with join info '{key}':{gwid}", devEUI, gatewayId);
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

                this.logger.LogDebug("got LogInfo '{key}':{gwid} attached gw: {desiredgw}", devEUI, gatewayId, joinInfo.DesiredGateway);
            }
            else
            {
                throw new JoinRefusedException("Failed to acquire lock for joininfo");
            }

            return joinInfo;
        }
    }
}
