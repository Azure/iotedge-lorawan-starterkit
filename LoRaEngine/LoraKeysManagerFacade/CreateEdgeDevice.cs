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
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Newtonsoft.Json;

    public class CreateEdgeDevice
    {
        private const string AbpDeviceId = "46AAC86800430028";
        private const string OtaaDeviceId = "47AAC86800430028";
        private readonly IDeviceRegistryManager registryManager;

        public CreateEdgeDevice(IDeviceRegistryManager registryManager)
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
            var apiUrl = new Uri($"https://{GetEnvironmentVariable("FACADE_HOST_NAME")}.scm.azurewebsites.net/api");
            var siteUrl = new Uri($"https://{GetEnvironmentVariable("FACADE_HOST_NAME")}.azurewebsites.net");

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

            try
            {
                await this.registryManager.CreateEdgeDeviceAsync(
                    deviceName,
                    deployEndDevice,
                    $"{siteUrl}/api/",
                    facadeKey,
                    region,
                    resetPin,
                    spiSpeed,
                    spiDev);
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
                    var abpDevice = await this.registryManager.GetDeviceAsync(DeviceFeedConstants.AbpDeviceId);
                    var otaaDevice = await this.registryManager.GetDeviceAsync(DeviceFeedConstants.OtaaDeviceId);

                    if (abpDevice == null || otaaDevice == null)
                    {
                        return PrepareResponse(HttpStatusCode.Conflict);
                    }
                }

                return PrepareResponse(HttpStatusCode.OK);
            }

            return PrepareResponse(HttpStatusCode.OK);
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
