
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
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace CreateDeviceFunction
{
    public static class CreateEdgeDevice
    {
        [FunctionName("CreateEdgeDevice")]
        public static async System.Threading.Tasks.Task<HttpResponseMessage> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            string connectionString = config.GetConnectionString("IoTHubConnectionString");
            string deviceConfigurationUrl = Environment.GetEnvironmentVariable("DEVICE_CONFIG_LOCATION");
            RegistryManager manager = RegistryManager.CreateFromConnectionString(connectionString);
            // parse query parameter
            var queryStrings=req.GetQueryParameterDictionary();
            string deviceName = "";
            string facadeKey = "";
            queryStrings.TryGetValue("deviceName", out deviceName);
            queryStrings.TryGetValue("facadeKey", out facadeKey);

            Device edgeGatewayDevice = new Device(deviceName);
            edgeGatewayDevice.Capabilities = new DeviceCapabilities()
            {
                IotEdge = true
            };
            await manager.AddDeviceAsync(edgeGatewayDevice);
            string json = "";
            //todo correct
            using (WebClient wc = new WebClient())
            {
                 json = wc.DownloadString(deviceConfigurationUrl);
            }
            ConfigurationContent spec = JsonConvert.DeserializeObject<ConfigurationContent>(json);
            await manager.AddModuleAsync(new Module(deviceName, "lorawannetworksrvmodule"));
            await manager.ApplyConfigurationContentOnDeviceAsync(deviceName, spec);

            Twin twin = new Twin();
            twin.Properties.Desired = new TwinCollection(@"{FacadeServerUrl:'" + String.Format("https://{0}.azurewebsites.net/api/", GetEnvironmentVariable("FACADE_HOST_NAME")) + "',FacadeAuthCode: " +
                "'" + facadeKey + "'}");
            var remoteTwin = await manager.GetTwinAsync(deviceName);

            await manager.UpdateTwinAsync(deviceName, "lorawannetworksrvmodule", twin, remoteTwin.ETag);

            bool deployEndDevice = false;
            Boolean.TryParse(Environment.GetEnvironmentVariable("DEPLOY_DEVICE"),out deployEndDevice);

            if (deployEndDevice)
            {
                Device endDevice = new Device("47AAC86800430028");
                await manager.AddDeviceAsync(endDevice);
                Twin endTwin = new Twin();
                endTwin.Tags = new TwinCollection(@"{AppEUI:'BE7A0000000014E2',AppKey:'8AFE71A145B253E49C3031AD068277A1',GatewayID:''," +
                "SensorDecoder:'DecoderRotatorySensor'}");
    
                var endRemoteTwin = await manager.GetTwinAsync(deviceName);
                await manager.UpdateTwinAsync("47AAC86800430028", endTwin, endRemoteTwin.ETag);

            }


            var template = @"{'$schema': 'https://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#', 'contentVersion': '1.0.0.0', 'parameters': {}, 'variables': {}, 'resources': []}";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            Console.WriteLine(template);

            response.Content = new StringContent(template, System.Text.Encoding.UTF8, "application/json");

            return response;
        }

        public static string GetEnvironmentVariable(string name)
        {
            return
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

    }


}





