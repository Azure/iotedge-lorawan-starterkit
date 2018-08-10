
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

namespace LoraKeysManagerFacade
{
    public class LoraDeviceInfo
    {
        public string DevAddr;
        public string DevEUI;
        public string AppKey;
        public string AppEUI;
        public string NwkSKey;
        public string AppSKey;
        public string PrimaryKey;
        public string AppNonce;
        public string DevNonce;
        public string NetId;
        public bool IsOurDevice = false;
        public bool IsJoinValid = false;
        public UInt16 FCntUp;
        public UInt16 FCntDown;
        public string GatewayID;
        public string SensorDecoder;
    }

  
    public static class NwkSKeyAppSKey
    {
        [FunctionName("GetNwkSKeyAppSKey")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log, ExecutionContext context)
        {
            return await KeysManager.GetKeys(req, log, context, true);
        }
    }

    public static class OTAAKeys
    {
        [FunctionName("PerformOTAA")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log, ExecutionContext context)
        {
            return await KeysManager.PerformOTAA(req, log, context, true);
        }
    }

    public static class KeysManager
    {

        public static async Task<IActionResult> GetKeys(HttpRequest req, TraceWriter log, ExecutionContext context,bool returnAppSKey )
        {
           

            string devAddr = req.Query["devAddr"];

            if (devAddr == null)
            {
                string errorMsg = "Missing devAddr in querystring";
                //log.Error(errorMsg);
                throw new Exception(errorMsg);
            }




            var config = new ConfigurationBuilder()
                          .SetBasePath(context.FunctionAppDirectory)
                          .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables()
                          .Build();
            string connectionString = config.GetConnectionString("IoTHubConnectionString");
            if (connectionString == null)
            {
                string errorMsg = "Missing IoTHubConnectionString in settings";
                //log.Error(errorMsg);
                throw new Exception(errorMsg);
            }

          
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);

            //Currently registry manager query only support select so we need to check for injection on the devaddr only for "'"
            //TODO check for sql injection
            devAddr =devAddr.Replace('\'',' ');

            var query = registryManager.CreateQuery($"SELECT * FROM devices WHERE tags.DevAddr = '{devAddr}'", 1);

            LoraDeviceInfo loraDeviceInfo = new LoraDeviceInfo();
            loraDeviceInfo.DevAddr = devAddr;

               

            while (query.HasMoreResults)
            {
                var page = await query.GetNextAsTwinAsync();
                //we query only for 1 result 
                foreach (var twin in page)
                {
                    loraDeviceInfo.DevEUI = twin.DeviceId;
                    if(returnAppSKey)
                        loraDeviceInfo.AppSKey = twin.Tags["AppSKey"].Value;
                    loraDeviceInfo.NwkSKey = twin.Tags["NwkSKey"].Value;
                    if (twin.Tags.Contains("GatewayID"))
                        loraDeviceInfo.GatewayID = twin.Tags["GatewayID"].Value;
                    if (twin.Tags.Contains("SensorDecoder"))
                        loraDeviceInfo.SensorDecoder = twin.Tags["SensorDecoder"].Value;

                    if (twin.Tags.Contains("AppEUI"))
                        loraDeviceInfo.AppEUI = twin.Tags["AppEUI"].Value;
                    loraDeviceInfo.IsOurDevice = true;
                    if (twin.Properties.Reported.Contains("FCntUp"))
                        loraDeviceInfo.FCntUp = twin.Properties.Reported["FCntUp"];
                    if (twin.Properties.Reported.Contains("FCntDown"))
                    {
                        loraDeviceInfo.FCntDown = twin.Properties.Reported["FCntDown"];
                    }
                        
                }
            }

            if (loraDeviceInfo.IsOurDevice)
            {
                var device = await registryManager.GetDeviceAsync(loraDeviceInfo.DevEUI);
                loraDeviceInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;
            }

            string json = JsonConvert.SerializeObject(loraDeviceInfo);

            return (ActionResult)new OkObjectResult(json);

            
        }

        public static async Task<IActionResult> PerformOTAA(HttpRequest req, TraceWriter log, ExecutionContext context, bool returnAppSKey)
        {

           


            string json;

            string AppKey;
            string AppSKey;
            string NwkSKey;
            string DevAddr;
            string DevNonce;
            string AppNonce;


            

            string devEUI = req.Query["devEUI"];

            if (devEUI == null)
            {
                string errorMsg = "Missing devEUI in querystring";
                //log.Error(errorMsg);
                throw new Exception(errorMsg);
            }

            string appEUI = req.Query["appEUI"];

            if (appEUI == null)
            {
                string errorMsg = "Missing appEUI in querystring";
                //log.Error(errorMsg);
                throw new Exception(errorMsg);
            }

            DevNonce = req.Query["devNonce"];

            if (DevNonce == null)
            {
                string errorMsg = "Missing devNonce in querystring";
                //log.Error(errorMsg);
                throw new Exception(errorMsg);
            }

            string GatewayID = req.Query["GatewayID"];

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = config.GetConnectionString("IoTHubConnectionString");

            if (connectionString == null)
            {
                string errorMsg = "Missing IoTHubConnectionString in settings";
                //log.Error(errorMsg);
                throw new Exception(errorMsg);
            }

           

                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connectionString);
              

                LoraDeviceInfo loraDeviceInfo = new LoraDeviceInfo();


                loraDeviceInfo.DevEUI = devEUI;
                                
                var twin = await registryManager.GetTwinAsync(devEUI);

                if (twin != null)
                {

                    loraDeviceInfo.IsOurDevice = true;

                    //Make sure that there is the AppEUI and it matches if not we cannot do the OTAA
                    if (!twin.Tags.Contains("AppEUI"))
                    {
                        string errorMsg = $"Missing AppEUI for OTAA for device {devEUI}";
                        //log.Error(errorMsg);
                        throw new Exception(errorMsg);
                    }
                    else
                    {
                        if (twin.Tags["AppEUI"].Value != appEUI)
                        {
                            string errorMsg = $"AppEUI for OTAA does not match for device {devEUI}";
                            //log.Error(errorMsg);
                            throw new Exception(errorMsg);
                        }
                    }

                    //Make sure that there is the AppKey if not we cannot do the OTAA
                    if (!twin.Tags.Contains("AppKey"))
                    {
                        string errorMsg = $"Missing AppKey for OTAA for device {devEUI}";
                        //log.Error(errorMsg);
                        throw new Exception(errorMsg);
                    }
                    else
                    {
                        AppKey = twin.Tags["AppKey"].Value;
                    }

                    //Make sure that is a new request and not a replay
                    if (twin.Tags.Contains("DevNonce"))
                    {
                        if (twin.Tags["DevNonce"] == DevNonce)
                        {
                            string errorMsg = $"DevNonce already used for device {devEUI}";
                            log.Info(errorMsg);
                            loraDeviceInfo.DevAddr = DevNonce;                            
                            loraDeviceInfo.IsJoinValid = false;
                            json = JsonConvert.SerializeObject(loraDeviceInfo);
                            return (ActionResult)new OkObjectResult(json);
                        }
                       
                    }

                    //Check that the device is joining throught the linked gateway and not another
                    if (twin.Tags.Contains("GatewayID"))
                    {
                       

                        if (!String.IsNullOrEmpty(twin.Tags["GatewayID"].Value) && twin.Tags["GatewayID"].Value.ToUpper() != GatewayID.ToUpper())
                        {
                            string errorMsg = $"Not the right gateway device-gateway:{twin.Tags["GatewayID"].Value} current-gateway:{GatewayID}";
                            log.Info(errorMsg);
                            loraDeviceInfo.DevAddr = DevNonce;
                            if (twin.Tags.Contains("GatewayID"))
                                loraDeviceInfo.GatewayID = twin.Tags["GatewayID"].Value;
                            loraDeviceInfo.IsJoinValid = false;
                            json = JsonConvert.SerializeObject(loraDeviceInfo);
                            return (ActionResult)new OkObjectResult(json);
                        }

                    }


                    byte[] netId = new byte[3] { 0, 0, 1 };

                    AppNonce = OTAAKeysGenerator.getAppNonce();

                    AppSKey = OTAAKeysGenerator.calculateKey( new byte[1]{ 0x02 }, OTAAKeysGenerator.StringToByteArray(AppNonce), netId, OTAAKeysGenerator.StringToByteArray(DevNonce), OTAAKeysGenerator.StringToByteArray(AppKey));
                    NwkSKey = OTAAKeysGenerator.calculateKey(new byte[1] { 0x01 }, OTAAKeysGenerator.StringToByteArray(AppNonce), netId, OTAAKeysGenerator.StringToByteArray(DevNonce), OTAAKeysGenerator.StringToByteArray(AppKey)); ;

                   
                    
                    //check that the devaddr is unique in the IoTHub registry
                    bool isDevAddrUnique = false;
                    
                    do
                    {
                        DevAddr = OTAAKeysGenerator.getDevAddr(netId);

                      

                        var query = registryManager.CreateQuery($"SELECT * FROM devices WHERE tags.DevAddr = '{DevAddr}'", 1);
                        if (query.HasMoreResults)
                        {
                            var page = await query.GetNextAsTwinAsync();
                            if(!page.GetEnumerator().MoveNext())
                                isDevAddrUnique = true;
                            else
                                isDevAddrUnique = false;
                        }
                        else
                        {
                            isDevAddrUnique = true;
                        }

                    } while (!isDevAddrUnique);

                                   

                    var patch = new
                    {
                        tags = new
                        {
                            AppSKey,
                            NwkSKey,
                            DevAddr,
                            DevNonce

                        }
                    };

                    await registryManager.UpdateTwinAsync(loraDeviceInfo.DevEUI, JsonConvert.SerializeObject(patch), twin.ETag);

                    loraDeviceInfo.DevAddr = DevAddr;
                    loraDeviceInfo.AppKey = twin.Tags["AppKey"].Value;
                    loraDeviceInfo.NwkSKey = NwkSKey;
                    loraDeviceInfo.AppSKey = AppSKey;
                    loraDeviceInfo.AppNonce = AppNonce;
                    loraDeviceInfo.AppEUI = appEUI;
                    loraDeviceInfo.NetId = BitConverter.ToString(netId).Replace("-", ""); ;

                    if (!returnAppSKey)
                        loraDeviceInfo.AppSKey = null;

                    //Accept the JOIN Request and the futher messages
                    loraDeviceInfo.IsJoinValid = true;

                    var device = await registryManager.GetDeviceAsync(loraDeviceInfo.DevEUI);
                    loraDeviceInfo.PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey;

                    if (twin.Tags.Contains("GatewayID"))
                        loraDeviceInfo.GatewayID = twin.Tags["GatewayID"].Value;
                    if (twin.Tags.Contains("SensorDecoder"))
                        loraDeviceInfo.SensorDecoder = twin.Tags["SensorDecoder"].Value;


            }
                else
                {
                    loraDeviceInfo.IsOurDevice = false;
                }
             

                json = JsonConvert.SerializeObject(loraDeviceInfo);
                return (ActionResult)new OkObjectResult(json);

           
        }

    }
}
