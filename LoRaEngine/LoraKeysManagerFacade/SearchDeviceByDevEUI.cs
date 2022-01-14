// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    public class SearchDeviceByDevEUI
    {
        private readonly IDeviceRegistryManager registryManager;

        public SearchDeviceByDevEUI(IDeviceRegistryManager registryManager)
        {
            this.registryManager = registryManager;
        }

        [FunctionName(nameof(GetDeviceByDevEUI))]
        public async Task<IActionResult> GetDeviceByDevEUI([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                log.LogError(ex, "Invalid version");
                return new BadRequestObjectResult(ex.Message);
            }

            return await RunGetDeviceByDevEUI(req, log);
        }

        private async Task<IActionResult> RunGetDeviceByDevEUI(HttpRequest req, ILogger log)
        {
            string devEui = req.Query["DevEUI"];
            if (!DevEui.TryParse(devEui, out var parsedDevEui))
            {
                return new BadRequestObjectResult("DevEUI missing or invalid.");
            }

            var device = await this.registryManager.GetDeviceAsync(parsedDevEui.ToString());
            if (device != null)
            {
                log.LogDebug($"Search for {devEui} found 1 device");
                return new OkObjectResult(new
                {
                    DevEUI = devEui,
                    device.PrimaryKey,
                    IoTHubHostname = device.AssignedIoTHub
                });
            }
            else
            {
                log.LogInformation($"Search for {devEui} found 0 devices");
                return new NotFoundResult();
            }
        }
    }
}
