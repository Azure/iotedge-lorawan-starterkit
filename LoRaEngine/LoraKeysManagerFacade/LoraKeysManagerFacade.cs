
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Devices;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace LoraKeysManagerFacade
{
    public class IoTHubDeviceInfo
    {
        public string DevAddr;
        public string DevEUI;
        public string PrimaryKey;
    }

    public class FCnt
    {
        public int FCntUp;
        public int FCntDown;
        public string GatewayId;
    }


    public static class DeviceGetter
    {
        static IDatabase redisCache;

        static RegistryManager registryManager;

        [FunctionName("GetDevice")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            //ABP Case
            string devAddr = req.Query["DevAddr"];
            //OTAA Case
            string devEUI = req.Query["DevEUI"];
            string devNonce = req.Query["DevNonce"];

            string gatewayId = req.Query["GatewayId"];

            if (redisCache == null || registryManager == null)
            {
                lock (typeof(FCntCacheChechk))
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

    public static class FCntCacheChechk
    {
      
        static IDatabase redisCache;
       

        [FunctionName("NextFCntDown")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            FCnt serverFCnt;

            string devEUI = req.Query["DevEUI"]; 
            string fCntDown = req.Query["FCntDown"];
            string fCntUp = req.Query["FCntUp"];  
            string gatewayId = req.Query["GatewayId"];
            string ABPFcntCacheReset = req.Query["ABPFcntCacheReset"];

            int newFCntDown=0;


            if (redisCache == null)
            {
                lock (typeof(FCntCacheChechk))
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
