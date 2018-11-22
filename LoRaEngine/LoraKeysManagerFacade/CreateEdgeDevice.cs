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
using System;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Text;

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
            string publishingUserName = "";
            string publishingPassword = "";
            string region="";
            string resetPin="";

            queryStrings.TryGetValue("deviceName", out deviceName);
            queryStrings.TryGetValue("publishingUserName", out publishingUserName);
            queryStrings.TryGetValue("publishingPassword", out publishingPassword);
            queryStrings.TryGetValue("region", out region);
            queryStrings.TryGetValue("resetPin", out resetPin);


            //Get function facade key
            var base64Auth = Convert.ToBase64String(Encoding.Default.GetBytes($"{publishingUserName}:{publishingPassword}"));
            var apiUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.scm.azurewebsites.net/api");
            var siteUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.azurewebsites.net");
            string JWT;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64Auth}");
                var result = client.GetAsync($"{apiUrl}/functions/admin/token").Result;
                JWT = result.Content.ReadAsStringAsync().Result.Trim('"'); //get  JWT for call funtion key
            }
            string facadeKey = "";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + JWT);

                string jsonResult = client.GetAsync($"{siteUrl}/admin/host/keys").Result.Content.ReadAsStringAsync().Result;
                dynamic resObject = JsonConvert.DeserializeObject(jsonResult);
                facadeKey = resObject.keys[0].value;
            }


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

            json = json.Replace("[$region]", region);
            json = json.Replace("[$reset_pin]", resetPin);
            ConfigurationContent spec = JsonConvert.DeserializeObject<ConfigurationContent>(json);
            await manager.AddModuleAsync(new Module(deviceName, "LoRaWanNetworkSrvModule"));
            
            await manager.ApplyConfigurationContentOnDeviceAsync(deviceName, spec);

            Twin twin = new Twin();
            twin.Properties.Desired = new TwinCollection(@"{FacadeServerUrl:'" + String.Format("https://{0}.azurewebsites.net/api/", GetEnvironmentVariable("FACADE_HOST_NAME")) + "',FacadeAuthCode: " +
                "'" + facadeKey + "'}");
            var remoteTwin = await manager.GetTwinAsync(deviceName);

            await manager.UpdateTwinAsync(deviceName, "LoRaWanNetworkSrvModule", twin, remoteTwin.ETag);

            bool deployEndDevice = false;
            Boolean.TryParse(Environment.GetEnvironmentVariable("DEPLOY_DEVICE"),out deployEndDevice);

            //This section will get deployed ONLY if the user selected the "deploy end device" options.
            //Information in this if clause, is for demo purpose only and should not be used for productive workloads.
            if (deployEndDevice)
            {
                string OTAAdeviceId = "47AAC86800430028";
                Device OTAADevice = new Device(OTAAdeviceId);
                await manager.AddDeviceAsync(OTAADevice);
                Twin OTAAendTwin = new Twin();
                OTAAendTwin.Properties.Desired = new TwinCollection(@"{AppEUI:'BE7A0000000014E2',AppKey:'8AFE71A145B253E49C3031AD068277A1',GatewayID:''," +
                "SensorDecoder:'DecoderValueSensor'}");
                var OTAARemoteTwin = await manager.GetTwinAsync(OTAAdeviceId);
                await manager.UpdateTwinAsync(OTAAdeviceId, OTAAendTwin, OTAARemoteTwin.ETag);
                string ABPdeviceId = "46AAC86800430028";
                Device ABPDevice = new Device(ABPdeviceId);
                await manager.AddDeviceAsync(ABPDevice);
                Twin ABPTwin = new Twin();
                ABPTwin.Properties.Desired = new TwinCollection(@"{AppSKey:'2B7E151628AED2A6ABF7158809CF4F3C',NwkSKey:'3B7E151628AED2A6ABF7158809CF4F3C',GatewayID:''," +
                "DevAddr:'0028B1B1',SensorDecoder:'DecoderValueSensor'}");
                var ABPRemoteTwin = await manager.GetTwinAsync(ABPdeviceId);
                await manager.UpdateTwinAsync(ABPdeviceId, ABPTwin, ABPRemoteTwin.ETag);
            }

            var template = @"{'$schema': 'https://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#', 'contentVersion': '1.0.0.0', 'parameters': {}, 'variables': {}, 'resources': []}";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            

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




