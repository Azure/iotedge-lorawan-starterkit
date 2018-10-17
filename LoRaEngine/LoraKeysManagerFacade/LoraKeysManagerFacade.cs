
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

namespace LoraKeysManagerFacade
{
    public class IoTHubDeviceInfo
    {
        public string DevAddr;
        public string DevEUI;
        public string PrimaryKey;
    }

  
    public static class DeviceGetter
    {
        [FunctionName("GetDevice")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log, ExecutionContext context)
        {
            //ABP Case
            string devAddr = req.Query["devAddr"];
            //OTAA Case
            string devEUI = req.Query["devEUI"];
            string devNonce = req.Query["devNonce"];
          

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

          

            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            var cache = redis.GetDatabase();


            RegistryManager registryManager;

            List<IoTHubDeviceInfo> results = new List<IoTHubDeviceInfo>();

            if (devEUI!=null)
            {

                string cacheKey = devEUI + devNonce;
                //check if we already got the same devEUI and devNonce it can be a reaply attack or a multigateway setup recieving the same join.We are rfusing all other than the first one.
                string cachedDevNonce = cache.StringGet(cacheKey, CommandFlags.DemandMaster);

                if (!String.IsNullOrEmpty(cachedDevNonce))
                {
                    return (ActionResult)new BadRequestObjectResult("UsedDevNonce");

                }

                cache.StringSet(cacheKey, devNonce,new TimeSpan(0,1,0), When.Always, CommandFlags.DemandMaster);
                

                //we connect here and not before we do the cache checking
                registryManager = RegistryManager.CreateFromConnectionString(connectionString);

                IoTHubDeviceInfo iotHubDeviceInfo = new IoTHubDeviceInfo();
                var device = await registryManager.GetDeviceAsync(devEUI);

                if (device != null)
                {
                    iotHubDeviceInfo.DevEUI = devEUI;
                    iotHubDeviceInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
                    results.Add(iotHubDeviceInfo);
                }
                                               
            }
            else if(devAddr!=null)
            {
                //TODO check for sql injection
                devAddr = devAddr.Replace('\'', ' ');

                //we connect here and not before we do the cache checking
                registryManager = RegistryManager.CreateFromConnectionString(connectionString);

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
