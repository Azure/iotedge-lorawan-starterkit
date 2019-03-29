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
                var results = await this.GetDeviceList(devEUI, gatewayId, devNonce, devAddr);
                string json = JsonConvert.SerializeObject(results);
                return new OkObjectResult(json);
            }
            catch (DeviceNonceUsedException)
            {
                return new BadRequestObjectResult("UsedDevNonce");
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
        }

        public async Task<List<IoTHubDeviceInfo>> GetDeviceList(string devEUI, string gatewayId, string devNonce, string devAddr)
        {
            var results = new List<IoTHubDeviceInfo>();

            if (devEUI != null)
            {
                // OTAA join
                string cacheKey = devEUI + devNonce;
                using (var deviceCache = new LoRaDeviceCache(this.cacheStore, devEUI, gatewayId, cacheKey))
                {
                    if (deviceCache.HasValue())
                    {
                        throw new DeviceNonceUsedException();
                    }

                    if (deviceCache.TryToLock(cacheKey + "joinlock"))
                    {
                        if (deviceCache.HasValue())
                        {
                            throw new DeviceNonceUsedException();
                        }

                        deviceCache.SetValue(devNonce, TimeSpan.FromMinutes(1));

                        var device = await this.registryManager.GetDeviceAsync(devEUI);

                        if (device != null)
                        {
                            var iotHubDeviceInfo = new IoTHubDeviceInfo
                            {
                                DevEUI = devEUI,
                                PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey
                            };
                            results.Add(iotHubDeviceInfo);

                            this.cacheStore.KeyDelete(devEUI);
                        }
                    }
                    else
                    {
                        throw new DeviceNonceUsedException();
                    }
                }
            }
            else if (devAddr != null)
            {
                // ABP or normal message

                // TODO check for sql injection
                devAddr = devAddr.Replace('\'', ' ');

                var query = this.registryManager.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{devAddr}' OR properties.reported.DevAddr ='{devAddr}'", 100);
                while (query.HasMoreResults)
                {
                    var page = await query.GetNextAsTwinAsync();

                    foreach (var twin in page)
                    {
                        if (twin.DeviceId != null)
                        {
                            var device = await this.registryManager.GetDeviceAsync(twin.DeviceId);
                            var iotHubDeviceInfo = new IoTHubDeviceInfo
                            {
                                DevAddr = devAddr,
                                DevEUI = twin.DeviceId,
                                PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey
                            };
                            results.Add(iotHubDeviceInfo);
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Missing devEUI or devAddr");
            }

            return results;
        }
    }
}
