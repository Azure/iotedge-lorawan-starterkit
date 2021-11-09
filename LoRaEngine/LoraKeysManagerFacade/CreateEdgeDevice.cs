// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
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
    using Newtonsoft.Json;

    public class CreateEdgeDevice
    {
        private const string AbpDeviceId = "46AAC86800430028";
        private const string OtaaDeviceId = "47AAC86800430028";
        private readonly RegistryManager registryManager;

        public CreateEdgeDevice(RegistryManager registryManager)
        {
            this.registryManager = registryManager;
        }

        [FunctionName(nameof(CreateEdgeDevice))]
        public async Task<HttpResponseMessage> CreateEdgeDeviceImp(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            // parse query parameter
            var queryStrings = req.GetQueryParameterDictionary();

            // required arguments
            if (!queryStrings.TryGetValue("deviceName", out var deviceName) ||
                !queryStrings.TryGetValue("publishingUserName", out var publishingUserName) ||
                !queryStrings.TryGetValue("publishingPassword", out var publishingPassword) ||
                !queryStrings.TryGetValue("region", out var region) ||
                !queryStrings.TryGetValue("resetPin", out var resetPin))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = "Missing required parameters." };
            }

            // optional arguments
            _ = queryStrings.TryGetValue("spiSpeed", out var spiSpeed);
            _ = queryStrings.TryGetValue("spiDev", out var spiDev);

            _ = bool.TryParse(Environment.GetEnvironmentVariable("DEPLOY_DEVICE"), out var deployEndDevice);

            // Get function facade key
            var base64Auth = Convert.ToBase64String(Encoding.Default.GetBytes($"{publishingUserName}:{publishingPassword}"));
            var apiUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.scm.azurewebsites.net/api");
            var siteUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.azurewebsites.net");
            string jwt;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64Auth}");
                var result = client.GetAsync(new Uri($"{apiUrl}/functions/admin/token")).Result;
                jwt = result.Content.ReadAsStringAsync().Result.Trim('"'); // get  JWT for call funtion key
            }

            var facadeKey = string.Empty;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + jwt);

                var jsonResult = client.GetAsync(new Uri($"{siteUrl}/admin/host/keys")).Result.Content.ReadAsStringAsync().Result;
                dynamic resObject = JsonConvert.DeserializeObject(jsonResult);
                facadeKey = resObject.keys[0].value;
            }

            var edgeGatewayDevice = new Device(deviceName)
            {
                Capabilities = new DeviceCapabilities()
                {
                    IotEdge = true
                }
            };

            try
            {
                _ = await this.registryManager.AddDeviceAsync(edgeGatewayDevice);

                var deviceConfigurationUrl = Environment.GetEnvironmentVariable("DEVICE_CONFIG_LOCATION");
                string json = null;

                // todo correct
                using (var wc = new WebClient())
                {
                    json = wc.DownloadString(deviceConfigurationUrl);
                }

                var appInsightsInstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                json = ReplaceJsonWithCorrectValues(region, resetPin, json, spiSpeed, spiDev, appInsightsInstrumentationKey);

                var spec = JsonConvert.DeserializeObject<ConfigurationContent>(json);
                _ = await this.registryManager.AddModuleAsync(new Module(deviceName, "LoRaWanNetworkSrvModule"));

                await this.registryManager.ApplyConfigurationContentOnDeviceAsync(deviceName, spec);

                var twin = new Twin();
                twin.Properties.Desired = new TwinCollection($"{{FacadeServerUrl:'https://{GetEnvironmentVariable("FACADE_HOST_NAME")}.azurewebsites.net/api/',FacadeAuthCode: '{facadeKey}'}}");
                var remoteTwin = await this.registryManager.GetTwinAsync(deviceName);

                _ = await this.registryManager.UpdateTwinAsync(deviceName, "LoRaWanNetworkSrvModule", twin, remoteTwin.ETag);

                // This section will get deployed ONLY if the user selected the "deploy end device" options.
                // Information in this if clause, is for demo purpose only and should not be used for productive workloads.
                if (deployEndDevice)
                {
                    var otaaDevice = new Device(OtaaDeviceId);

                    _ = await this.registryManager.AddDeviceAsync(otaaDevice);

                    var otaaEndTwin = new Twin();
                    otaaEndTwin.Properties.Desired = new TwinCollection(@"{AppEUI:'BE7A0000000014E2',AppKey:'8AFE71A145B253E49C3031AD068277A1',GatewayID:'',SensorDecoder:'DecoderValueSensor'}");
                    var otaaRemoteTwin = _ = await this.registryManager.GetTwinAsync(OtaaDeviceId);
                    _ = await this.registryManager.UpdateTwinAsync(OtaaDeviceId, otaaEndTwin, otaaRemoteTwin.ETag);

                    var abpDevice = new Device(AbpDeviceId);
                    _ = await this.registryManager.AddDeviceAsync(abpDevice);
                    var abpTwin = new Twin();
                    abpTwin.Properties.Desired = new TwinCollection(@"{AppSKey:'2B7E151628AED2A6ABF7158809CF4F3C',NwkSKey:'3B7E151628AED2A6ABF7158809CF4F3C',GatewayID:'',DevAddr:'0228B1B1',SensorDecoder:'DecoderValueSensor'}");
                    var abpRemoteTwin = await this.registryManager.GetTwinAsync(AbpDeviceId);
                    _ = await this.registryManager.UpdateTwinAsync(AbpDeviceId, abpTwin, abpRemoteTwin.ETag);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types. This will go away when we implement #242
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // In case of an exception in device provisioning we want to make sure that we return a proper template if our devices are successfullycreated
                var edgeGateway = await this.registryManager.GetDeviceAsync(deviceName);

                if (edgeGateway == null)
                {
                    return PrepareResponse(HttpStatusCode.Conflict);
                }

                if (deployEndDevice)
                {
                    var abpDevice = await this.registryManager.GetDeviceAsync(AbpDeviceId);
                    var otaaDevice = await this.registryManager.GetDeviceAsync(OtaaDeviceId);

                    if (abpDevice == null || otaaDevice == null)
                    {
                        return PrepareResponse(HttpStatusCode.Conflict);
                    }
                }

                return PrepareResponse(HttpStatusCode.OK);
            }

            return PrepareResponse(HttpStatusCode.OK);
        }

        private static string ReplaceJsonWithCorrectValues(string region, string resetPin, string json, string spiSpeed, string spiDev, string appInsightsInstrumentationKey)
        {
            json = json.Replace("[$region]", region, StringComparison.Ordinal);
            json = json.Replace("[$reset_pin]", resetPin, StringComparison.Ordinal);
            json = json.Replace("[$appinsights_instrumentationkey]", appInsightsInstrumentationKey, StringComparison.Ordinal);

            if (string.Equals(spiSpeed, "8", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(spiSpeed))
            {
                // default case
                json = json.Replace("[$spi_speed]", string.Empty, StringComparison.Ordinal);
            }
            else
            {
                json = json.Replace(
                    "[$spi_speed]",
                    ",'SPI_SPEED':{'value':'2'}",
                    StringComparison.Ordinal);
            }

            if (string.Equals(spiDev, "2", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(spiDev))
            {
                // default case
                json = json.Replace("[$spi_dev]", string.Empty, StringComparison.Ordinal);
            }
            else
            {
                json = json.Replace(
                    "[$spi_dev]",
                    ",'SPI_DEV':{'value':'1'}",
                    StringComparison.Ordinal);
            }

            return json;
        }

        private static HttpResponseMessage PrepareResponse(HttpStatusCode httpStatusCode)
        {
            var template = @"{'$schema': 'https://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#', 'contentVersion': '1.0.0.0', 'parameters': {}, 'variables': {}, 'resources': []}";
            var response = new HttpResponseMessage(httpStatusCode);
            if (httpStatusCode == HttpStatusCode.OK)
            {
                response.Content = new StringContent(template, Encoding.UTF8, "application/json");
            }

            return response;
        }

        public static string GetEnvironmentVariable(string name)
        {
            return
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }
}
