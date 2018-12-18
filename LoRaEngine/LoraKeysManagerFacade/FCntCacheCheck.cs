//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Linq;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using LoRaWan.Shared;

namespace LoraKeysManagerFacade
{
    public class FCnt
    {
        public int FCntUp;
        public int FCntDown;
        public string GatewayId;
    }

    public static class FCntCacheCheck
    {
      
        static IDatabase redisCache;
       
        [FunctionName(nameof(NextFCntDown))]
        public static async Task<IActionResult> NextFCntDown([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            return await Run(req, log, context, ApiVersion.LatestVersion);
        }

        public static async Task<IActionResult> Run(HttpRequest req, ILogger log, ExecutionContext context, ApiVersion currentApiVersion)
        {
            // set the current version in the response header
            req.HttpContext.Response.Headers.Add(ApiVersion.HttpHeaderName, currentApiVersion.Version);

            var requestedVersion = req.GetRequestedVersion();
            if (requestedVersion == null || !currentApiVersion.SupportsVersion(requestedVersion))
            {
                return new BadRequestObjectResult($"Incompatible versions (requested: '{requestedVersion.Name ?? string.Empty}', current: '{currentApiVersion.Name}')");
            }

            FCnt serverFCnt;
            string devEUI = req.Query["DevEUI"]; 
            string fCntDown = req.Query["FCntDown"];
            string fCntUp = req.Query["FCntUp"];  
            string gatewayId = req.Query["GatewayId"];
            string ABPFcntCacheReset = req.Query["ABPFcntCacheReset"];
            int newFCntDown=0;
            if (redisCache == null)
            {
                lock (typeof(FCntCacheCheck))
                {
                    if (redisCache == null)
                    {
                       
                        var config = new ConfigurationBuilder()
                                    .SetBasePath(context.FunctionAppDirectory)
                                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                                    .AddEnvironmentVariables()
                                    .Build();
              
                        var redisConnectionString = config.GetConnectionString("RedisConnectionString");


                        if (string.IsNullOrEmpty(redisConnectionString))
                        {
                            string errorMsg = "Missing RedisConnectionString in settings";
                            throw new Exception(errorMsg);
                        }

                        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                        redisCache = redis.GetDatabase();
                    }
                }

            }

            if(!string.IsNullOrEmpty(ABPFcntCacheReset))
            {
                redisCache.KeyDelete(devEUI);
                return (ActionResult)new OkObjectResult(null);
            }

            if (!String.IsNullOrEmpty(devEUI) && !String.IsNullOrEmpty(fCntDown) && !String.IsNullOrEmpty(fCntUp) && !String.IsNullOrEmpty(gatewayId))
            {
                int clientFCntDown = int.Parse(fCntDown);
                int clientFCntUp = int.Parse(fCntUp);

                string cacheKey = devEUI;

                try
                {
                    if (redisCache.LockTake(cacheKey + "msglock", gatewayId, new TimeSpan(0, 0, 10)))
                    {
                        string cachedFCnt = redisCache.StringGet(cacheKey, CommandFlags.DemandMaster);
                        //we have it cached
                        if (!string.IsNullOrEmpty(cachedFCnt))
                        {

                            serverFCnt = (FCnt)JsonConvert.DeserializeObject(cachedFCnt, typeof(FCnt));
                            //it is a new message coming up by the first gateway 
                            if (clientFCntUp > serverFCnt.FCntUp)
                            {
                                if (clientFCntDown >= serverFCnt.FCntDown)
                                    newFCntDown = (int)(clientFCntDown + 1);
                                else
                                    newFCntDown = (int)(serverFCnt.FCntDown + 1);

                                serverFCnt.FCntUp = clientFCntUp;
                                serverFCnt.FCntDown = newFCntDown;
                                serverFCnt.GatewayId = gatewayId;

                                CacheFcnt(serverFCnt, redisCache, cacheKey);

                            }
                            //it is a retry message coming up by the same first gateway 
                            else if (clientFCntUp == serverFCnt.FCntUp && gatewayId == serverFCnt.GatewayId)
                            {
                                newFCntDown = serverFCnt.FCntDown + 1;
                                serverFCnt.FCntDown = newFCntDown;

                                CacheFcnt(serverFCnt, redisCache, cacheKey);
                            }
                            else
                            {
                                //we tell not to send any ack or downstream msg
                                newFCntDown = 0;
                            }
                        }
                        //it is the first message from this device
                        else
                        {

                            newFCntDown = clientFCntDown + 1;
                            serverFCnt = new FCnt();
                            serverFCnt.FCntDown = newFCntDown;
                            serverFCnt.FCntUp = clientFCntUp;
                            serverFCnt.GatewayId = gatewayId;

                            CacheFcnt(serverFCnt, redisCache, cacheKey);
                        }
                     }
                }
                finally
                {
                    redisCache.LockRelease(cacheKey + "msglock", gatewayId);
                }
            }
            else
            {
                string errorMsg = "Missing DevEUI or FCntDown or FCntUp or GatewayId";
                throw new Exception(errorMsg);
            }          
            return (ActionResult)new OkObjectResult(newFCntDown);
        }

        private static void CacheFcnt(FCnt serverFCnt, IDatabase cache, string cacheKey)
        {
            cache.StringSet(cacheKey, JsonConvert.SerializeObject(serverFCnt), new TimeSpan(30, 0, 0, 0), When.Always, CommandFlags.DemandMaster);           
        }
    }
}
