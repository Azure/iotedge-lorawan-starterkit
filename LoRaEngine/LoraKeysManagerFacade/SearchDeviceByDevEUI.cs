// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public class SearchDeviceByDevEUI
    {
        private readonly RegistryManager registryManager;

        public SearchDeviceByDevEUI(RegistryManager registryManager)
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
            var rawDevEui = req.Query["DevEUI"];
            if (StringValues.IsNullOrEmpty(rawDevEui))
            {
                log.LogError("DevEUI missing in request");
                return new BadRequestObjectResult("DevEUI missing in request");
            }
            var devEui = DevEui.Parse(rawDevEui.ToString());

            var result = new List<IoTHubDeviceInfo>();
            var device = await this.registryManager.GetDeviceAsync(devEui.AsIotHubDeviceId());
            if (device != null)
            {
                result.Add(new IoTHubDeviceInfo()
                {
                    DevEUI = devEui,
                    PrimaryKey = device.Authentication.SymmetricKey.PrimaryKey
                });

                log.LogDebug($"Search for {rawDevEui} found 1 device");
                return new OkObjectResult(result);
            }
            else
            {
                log.LogInformation($"Search for {rawDevEui} found 0 devices");
                return new NotFoundObjectResult(result);
            }
        }
    }
}
