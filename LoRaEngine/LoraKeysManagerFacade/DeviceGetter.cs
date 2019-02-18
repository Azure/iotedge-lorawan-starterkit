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
        static RegistryManager registryManager;
        static object registrySingletonLock = new object();

        public class IoTHubDeviceInfo
        {
            public string DevAddr { get; set; }

            public string DevEUI { get; set; }

            public string PrimaryKey { get; set; }
        }

        /// <summary>
        /// Entry point function for getting devices
        /// </summary>
        [FunctionName(nameof(GetDevice))]
        public static async Task<IActionResult> GetDevice([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            return await Run(req, log, context, ApiVersion.LatestVersion);
        }

        // Runner
        public static async Task<IActionResult> Run(HttpRequest req, ILogger log, ExecutionContext context, ApiVersion currentApiVersion)
        {
            // Set the current version in the response header
            req.HttpContext.Response.Headers.Add(ApiVersion.HttpHeaderName, currentApiVersion.Version);

            var requestedVersion = req.GetRequestedVersion();
            if (requestedVersion == null || !currentApiVersion.SupportsVersion(requestedVersion))
            {
                return new BadRequestObjectResult($"Incompatible versions (requested: '{requestedVersion.Name ?? string.Empty}', current: '{currentApiVersion.Name}')");
            }

            // ABP Case
            string devAddr = req.Query["DevAddr"];
            // OTAA Case
            string devEUI = req.Query["DevEUI"];
            string devNonce = req.Query["DevNonce"];

            string gatewayId = req.Query["GatewayId"];

            EnsureRegistryManager(context);

            List<IoTHubDeviceInfo> results = new List<IoTHubDeviceInfo>();

            // OTAA join
            if (devEUI != null)
            {
                string cacheKey = devEUI + devNonce;
                using (var deviceCache = LoRaDeviceCache.Create(context, devEUI, gatewayId, cacheKey))
                {
                    if (deviceCache.TryToLock(cacheKey + "joinlock"))
                    {
                        if (deviceCache.TryGetValue(out _))
                        {
                            return (ActionResult)new BadRequestObjectResult("UsedDevNonce");
                        }

                        deviceCache.SetValue(devNonce, TimeSpan.FromMinutes(1));

                        var iotHubDeviceInfo = new IoTHubDeviceInfo();
                        var device = await registryManager.GetDeviceAsync(devEUI);

                        if (device != null)
                        {
                            iotHubDeviceInfo.DevEUI = devEUI;
                            iotHubDeviceInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                            results.Add(iotHubDeviceInfo);

                            // clear device FCnt cache after join
                            LoRaDeviceCache.Delete(devEUI, context);
                        }
                    }
                }
            }

            // ABP or normal message
            else if (devAddr != null)
            {
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
                            var iotHubDeviceInfo = new IoTHubDeviceInfo
                            {
                                DevAddr = devAddr
                            };

                            var device = await registryManager.GetDeviceAsync(twin.DeviceId);
                            iotHubDeviceInfo.DevEUI = twin.DeviceId;
                            iotHubDeviceInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                            results.Add(iotHubDeviceInfo);
                        }
                    }
                }
            }
            else
            {
                string errorMsg = "Missing devEUI or devAddr";
                throw new Exception(errorMsg);
            }

            string json = JsonConvert.SerializeObject(results);
            return (ActionResult)new OkObjectResult(json);
        }

        private static void EnsureRegistryManager(ExecutionContext context)
        {
            if (registryManager == null)
            {
                lock (registrySingletonLock)
                {
                    if (registryManager == null)
                    {
                        var config = new ConfigurationBuilder()
                          .SetBasePath(context.FunctionAppDirectory)
                          .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables()
                          .Build();
                        string connectionString = config.GetConnectionString("IoTHubConnectionString");

                        if (connectionString == null)
                        {
                            string errorMsg = "Missing IoTHubConnectionString in settings";
                            throw new Exception(errorMsg);
                        }

                        registryManager = RegistryManager.CreateFromConnectionString(connectionString);
                    }
                }
            }
        }
    }
}
