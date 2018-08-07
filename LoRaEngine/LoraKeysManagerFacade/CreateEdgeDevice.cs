
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using System.Collections.Generic;

namespace CreateDeviceFunction
{
    public static class CreateEdgeDevice
    {
        [FunctionName("CreateEdgeDevice")]
        public static async System.Threading.Tasks.Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            string connectionString = Environment.GetEnvironmentVariable("IOT_HUB_OWNER_CONNECTION_STRING");

            RegistryManager manager = RegistryManager.CreateFromConnectionString(connectionString);

            string deviceName = req.Query["deviceName"];

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            Device d = new Device(deviceName);
            d.Capabilities = new DeviceCapabilities()
            {
                IotEdge = true
            };
            await manager.AddDeviceAsync(d);
            ConfigurationContent spec = JsonConvert.DeserializeObject<ConfigurationContent>(File.ReadAllText("./config.json"));
            await manager.AddModuleAsync(new Module(deviceName, "lorawannetworksrvmodule"));
            await manager.ApplyConfigurationContentOnDeviceAsync(deviceName, spec);

            Twin twin = new Twin();
            twin.Properties.Desired = new TwinCollection(@"{FacadeServerUrl:'" + String.Format("https://{0}.azurewebsites.net/api/", GetEnvironmentVariable("FACADE_HOST_NAME")) + "',FacadeAuthCode: " +
                "'" + GetEnvironmentVariable("FACADE_KEY") + "'}");
            var remoteTwin = await manager.GetTwinAsync(deviceName);

            await manager.UpdateTwinAsync(deviceName, "lorawannetworksrvmodule", twin, remoteTwin.ETag);


            return new CreatedResult("IoTHub", d);
        }

        public static string GetEnvironmentVariable(string name)
        {
            return
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

    }


}





