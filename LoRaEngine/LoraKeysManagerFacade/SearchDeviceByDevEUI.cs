// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    public class SearchDeviceByDevEUI
    {
        private readonly IDeviceRegistryManager registryManager;
        private readonly ILogger<SearchDeviceByDevEUI> logger;

        public SearchDeviceByDevEUI(IDeviceRegistryManager registryManager, ILogger<SearchDeviceByDevEUI> logger)
        {
            this.registryManager = registryManager;
            this.logger = logger;
        }

        [FunctionName(nameof(GetDeviceByDevEUI))]
        public async Task<IActionResult> GetDeviceByDevEUI([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                this.logger.LogError(ex, "Invalid version");
                return new BadRequestObjectResult(ex.Message);
            }

            return await RunGetDeviceByDevEUI(req);
        }

        private async Task<IActionResult> RunGetDeviceByDevEUI(HttpRequest req)
        {
            string devEui = req.Query["DevEUI"];
            if (!DevEui.TryParse(devEui, out var parsedDevEui))
            {
                return new BadRequestObjectResult("DevEUI missing or invalid.");
            }

            using var deviceScope = this.logger.BeginDeviceScope(parsedDevEui);

            var primaryKey = await this.registryManager.GetDevicePrimaryKeyAsync(parsedDevEui.ToString());
            if (primaryKey != null)
            {
                this.logger.LogDebug($"Search for {devEui} found 1 device");
                return new OkObjectResult(new
                {
                    DevEUI = devEui,
                    PrimaryKey = primaryKey
                });
            }
            else
            {
                this.logger.LogInformation($"Search for {devEui} found 0 devices");
                return new NotFoundResult();
            }
        }
    }
}
