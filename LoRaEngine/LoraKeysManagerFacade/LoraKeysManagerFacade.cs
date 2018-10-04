
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
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);

            List<IoTHubDeviceInfo> results = new List<IoTHubDeviceInfo>();

            if (devEUI!=null)
            {
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
