//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Devices;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using LoRaWan.Shared;

namespace LoraKeysManagerFacade
{
    public static class DeviceGetter
    {
        static IDatabase redisCache;

        static RegistryManager registryManager;

        public class IoTHubDeviceInfo
        {
            public string DevAddr;
            public string DevEUI;
            public string PrimaryKey;
        }

        /// <summary>
        /// Entry point function for getting devices
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <param name="context"></param>
        /// <param name="currentApiVersion"></param>
        /// <returns></returns>
        [FunctionName(nameof(GetDevice))]
        public static async Task<IActionResult> GetDevice([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            return await Run(req, log, context, ApiVersion.LatestVersion);

        }

        // Runner
        public static async Task<IActionResult> Run(HttpRequest req, ILogger log, ExecutionContext context, ApiVersion currentApiVersion)
        {
            // set the current version in the response header
            req.HttpContext.Response.Headers.Add(ApiVersion.HttpHeaderName, currentApiVersion.Version);

            var requestedVersion = req.GetRequestedVersion();
            if (requestedVersion == null || !currentApiVersion.SupportsVersion(requestedVersion))
            {
                return new BadRequestObjectResult($"Incompatible versions (requested: '{requestedVersion.Name ?? string.Empty}', current: '{currentApiVersion.Name}')");
            }
        

            //ABP Case
            string devAddr = req.Query["DevAddr"];
            //OTAA Case
            string devEUI = req.Query["DevEUI"];
            string devNonce = req.Query["DevNonce"];

            string gatewayId = req.Query["GatewayId"];

            if (redisCache == null || registryManager == null)
            {
                lock (typeof(FCntCacheCheck))
                {
                    if (redisCache == null || registryManager == null)
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

                        string redisConnectionString = config.GetConnectionString("RedisConnectionString");
                        if (redisConnectionString == null)
                        {
                            string errorMsg = "Missing RedisConnectionString in settings";
                            throw new Exception(errorMsg);
                        }

                        registryManager = RegistryManager.CreateFromConnectionString(connectionString);

                        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                        redisCache = redis.GetDatabase();
                    }
                }


            }


            List<IoTHubDeviceInfo> results = new List<IoTHubDeviceInfo>();

            //OTAA join
            if (devEUI!=null)
            {

                string cacheKey = devEUI + devNonce;

                try
                {

                    if (redisCache.LockTake(cacheKey + "joinlock", gatewayId, new TimeSpan(0, 0, 10)))
                    {
                        //check if we already got the same devEUI and devNonce it can be a reaply attack or a multigateway setup recieving the same join.We are rfusing all other than the first one.

                        string cachedDevNonce = redisCache.StringGet(cacheKey, CommandFlags.DemandMaster);

                        if (!String.IsNullOrEmpty(cachedDevNonce))
                        {
                            return (ActionResult)new BadRequestObjectResult("UsedDevNonce");

                        }

                        redisCache.StringSet(cacheKey, devNonce, new TimeSpan(0, 1, 0), When.Always, CommandFlags.DemandMaster);

                        IoTHubDeviceInfo iotHubDeviceInfo = new IoTHubDeviceInfo();
                        var device = await registryManager.GetDeviceAsync(devEUI);

                        if (device != null)
                        {
                            iotHubDeviceInfo.DevEUI = devEUI;
                            iotHubDeviceInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                            results.Add(iotHubDeviceInfo);

                            //clear device FCnt cache after join
                            redisCache.KeyDelete(devEUI);
                        }
                    }
                }
                finally
                {
                    redisCache.LockRelease(cacheKey + "joinlock", gatewayId);
                }
                                               
            }
            //ABP or normal message
            else if(devAddr!=null)
            {
                //TODO check for sql injection
                devAddr = devAddr.Replace('\'', ' ');
            
                var query = registryManager.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{devAddr}' OR properties.reported.DevAddr ='{devAddr}'", 100);
                while (query.HasMoreResults)
                {
                    var page = await query.GetNextAsTwinAsync();

                    foreach (var twin in page)
                    {
                        if (twin.DeviceId != null)
                        {
                            IoTHubDeviceInfo iotHubDeviceInfo = new IoTHubDeviceInfo();
                            iotHubDeviceInfo.DevAddr = devAddr;
                            var device = await registryManager.GetDeviceAsync(twin.DeviceId);
                            iotHubDeviceInfo.DevEUI = twin.DeviceId;
                            iotHubDeviceInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                            results.Add(iotHubDeviceInfo);
                            break;
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
    }
}
