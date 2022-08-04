// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    public class CreateEdgeDevice
    {
        private readonly IDeviceRegistryManager registryManager;
        private readonly IHttpClientFactory httpClientFactory;

        public CreateEdgeDevice(IDeviceRegistryManager registryManager, IHttpClientFactory httpClientFactory)
        {
            this.registryManager = registryManager;
            this.httpClientFactory = httpClientFactory;
        }

        [FunctionName(nameof(CreateEdgeDevice))]
        public async Task<HttpResponseMessage> CreateEdgeDeviceImp(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // parse query parameter
            var queryStrings = req.GetQueryParameterDictionary();

            // required arguments
            if (!queryStrings.TryGetValue("deviceName", out var deviceName) ||
                !queryStrings.TryGetValue("publishingUserName", out var publishingUserName) ||
                !queryStrings.TryGetValue("publishingPassword", out var publishingPassword) ||
                !queryStrings.TryGetValue("region", out var region) ||
                !queryStrings.TryGetValue("stationEui", out var stationEuiString) ||
                !queryStrings.TryGetValue("resetPin", out var resetPin))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = "Missing required parameters." };
            }

            if (!StationEui.TryParse(stationEuiString, out _))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { ReasonPhrase = "Station EUI could not be properly parsed." };
            }

            // optional arguments
            _ = queryStrings.TryGetValue("spiSpeed", out var spiSpeed);
            _ = queryStrings.TryGetValue("spiDev", out var spiDev);

            _ = bool.TryParse(Environment.GetEnvironmentVariable("DEPLOY_DEVICE"), out var deployEndDevice);

            try
            {
                await this.registryManager.DeployEdgeDeviceAsync(deviceName, resetPin, spiSpeed, spiDev, publishingUserName, publishingPassword);

                await this.registryManager.DeployConcentratorAsync(stationEuiString, region);

                // This section will get deployed ONLY if the user selected the "deploy end device" options.
                // Information in this if clause, is for demo purpose only and should not be used for productive workloads.
                if (deployEndDevice)
                {
                    _ = await this.registryManager.DeployEndDevicesAsync();
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types. This will go away when we implement #242
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                log.LogWarning(ex.Message);

                // In case of an exception in device provisioning we want to make sure that we return a proper template if our devices are successfullycreated
                var edgeGateway = await this.registryManager.GetTwinAsync(deviceName);

                if (edgeGateway == null)
                {
                    return PrepareResponse(HttpStatusCode.Conflict);
                }

                if (deployEndDevice && !await this.registryManager.DeployEndDevicesAsync())
                {
                    return PrepareResponse(HttpStatusCode.Conflict);
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
    }
}
