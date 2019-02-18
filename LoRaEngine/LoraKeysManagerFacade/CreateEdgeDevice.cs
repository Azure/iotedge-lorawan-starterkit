// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CreateDeviceFunction
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Build.Framework;
    using Newtonsoft.Json;

    public static class CreateEdgeDevice
    {
        [FunctionName("CreateEdgeDevice")]
        public static async Task<HttpResponseMessage> CreateEdgeDeviceImp([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            var manager = LoraKeysManagerFacade.LoRaRegistryManager.GetCurrentInstance(context.FunctionAppDirectory);

            // parse query parameter
            var queryStrings = req.GetQueryParameterDictionary();

            queryStrings.TryGetValue("deviceName", out string deviceName);
            queryStrings.TryGetValue("publishingUserName", out string publishingUserName);
            queryStrings.TryGetValue("publishingPassword", out string publishingPassword);
            queryStrings.TryGetValue("region", out string region);
            queryStrings.TryGetValue("resetPin", out string resetPin);

            // Get function facade key
            var base64Auth = Convert.ToBase64String(Encoding.Default.GetBytes($"{publishingUserName}:{publishingPassword}"));
            var apiUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.scm.azurewebsites.net/api");
            var siteUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.azurewebsites.net");
            string jwt;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64Auth}");
                var result = client.GetAsync($"{apiUrl}/functions/admin/token").Result;
                jwt = result.Content.ReadAsStringAsync().Result.Trim('"'); // get  JWT for call funtion key
            }

            string facadeKey = string.Empty;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + jwt);

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

            string deviceConfigurationUrl = Environment.GetEnvironmentVariable("DEVICE_CONFIG_LOCATION");
            string json = null;

            // todo correct
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
            twin.Properties.Desired = new TwinCollection(@"{FacadeServerUrl:'" + string.Format("https://{0}.azurewebsites.net/api/", GetEnvironmentVariable("FACADE_HOST_NAME")) + "',FacadeAuthCode: " +
                "'" + facadeKey + "'}");
            var remoteTwin = await manager.GetTwinAsync(deviceName);

            await manager.UpdateTwinAsync(deviceName, "LoRaWanNetworkSrvModule", twin, remoteTwin.ETag);

            bool.TryParse(Environment.GetEnvironmentVariable("DEPLOY_DEVICE"), out bool deployEndDevice);

            // This section will get deployed ONLY if the user selected the "deploy end device" options.
            // Information in this if clause, is for demo purpose only and should not be used for productive workloads.
            if (deployEndDevice)
            {
                string otaaDeviceId = "47AAC86800430028";
                var otaaDevice = new Device(otaaDeviceId);
                await manager.AddDeviceAsync(otaaDevice);

                var otaaEndTwin = new Twin();
                otaaEndTwin.Properties.Desired = new TwinCollection(@"{AppEUI:'BE7A0000000014E2',AppKey:'8AFE71A145B253E49C3031AD068277A1',GatewayID:'',SensorDecoder:'DecoderValueSensor'}");
                var otaaRemoteTwin = await manager.GetTwinAsync(otaaDeviceId);
                await manager.UpdateTwinAsync(otaaDeviceId, otaaEndTwin, otaaRemoteTwin.ETag);

                string abpDeviceId = "46AAC86800430028";
                var abpDevice = new Device(abpDeviceId);
                await manager.AddDeviceAsync(abpDevice);
                var abpTwin = new Twin();
                abpTwin.Properties.Desired = new TwinCollection(@"{AppSKey:'2B7E151628AED2A6ABF7158809CF4F3C',NwkSKey:'3B7E151628AED2A6ABF7158809CF4F3C',GatewayID:'',DevAddr:'0228B1B1',SensorDecoder:'DecoderValueSensor'}");
                var abpRemoteTwin = await manager.GetTwinAsync(abpDeviceId);
                await manager.UpdateTwinAsync(abpDeviceId, abpTwin, abpRemoteTwin.ETag);
            }

            var template = @"{'$schema': 'https://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#', 'contentVersion': '1.0.0.0', 'parameters': {}, 'variables': {}, 'resources': []}";
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);

            response.Content = new StringContent(template, Encoding.UTF8, "application/json");

            return response;
        }

        public static string GetEnvironmentVariable(string name)
        {
            return
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }
}
