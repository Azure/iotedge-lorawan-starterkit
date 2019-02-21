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

    public static class DeviceGetter
    {
        /// <summary>
        /// Entry point function for getting devices
        /// </summary>
        [FunctionName(nameof(GetDevice))]
        public static async Task<IActionResult> GetDevice([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex);
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
                var results = await GetDeviceList(devEUI, gatewayId, devNonce, devAddr, context);
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

        public static async Task<List<IoTHubDeviceInfo>> GetDeviceList(string devEUI, string gatewayId, string devNonce, string devAddr, ExecutionContext context)
        {
            var results = new List<IoTHubDeviceInfo>();
            var registryManager = LoRaRegistryManager.GetCurrentInstance(context.FunctionAppDirectory);

            if (devEUI != null)
            {
                // OTAA join
                string cacheKey = devEUI + devNonce;
                using (var deviceCache = LoRaDeviceCache.Create(context, devEUI, gatewayId, cacheKey))
                {
                    if (deviceCache.TryToLock(cacheKey + "joinlock"))
                    {
                        if (deviceCache.TryGetValue(out _))
                        {
                            throw new DeviceNonceUsedException();
                        }

                        deviceCache.SetValue(devNonce, TimeSpan.FromMinutes(1));

                        var device = await registryManager.GetDeviceAsync(devEUI);

                        if (device != null)
                        {
                            var iotHubDeviceInfo = new IoTHubDeviceInfo
                            {
                                DevEUI = devEUI,
                                PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey
                            };
                            results.Add(iotHubDeviceInfo);

                            // clear device FCnt cache after join
                            LoRaDeviceCache.Delete(devEUI, context);
                        }
                    }
                }
            }
            else if (devAddr != null)
            {
                // ABP or normal message

                // TODO check for sql injection
                devAddr = devAddr.Replace('\'', ' ');

                var query = registryManager.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{devAddr}' OR properties.reported.DevAddr ='{devAddr}'", 100);
                while (query.HasMoreResults)
                {
                    var page = await query.GetNextAsTwinAsync();

                    foreach (var twin in page)
                    {
                        if (twin.DeviceId != null)
                        {
                            var device = await registryManager.GetDeviceAsync(twin.DeviceId);
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
