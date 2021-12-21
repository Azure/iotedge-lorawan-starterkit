// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public class ClassCDeviceMessageSender
    {
        private readonly RegistryManager registryManager;
        private readonly ILoRaDeviceCacheStore cacheStore;

        public ClassCDeviceMessageSender(RegistryManager registryManager, ILoRaDeviceCacheStore cacheStore)
        {
            this.registryManager = registryManager;
            this.cacheStore = cacheStore;
        }

        /// <summary>
        /// Entry point function for sending a message to a class C device.
        /// </summary>
        [FunctionName(nameof(SendToClassCDevice))]
        public async Task<IActionResult> SendToClassCDevice(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
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

            return await RunSendToClassCDevice(req, log);
        }

        private async Task<IActionResult> RunSendToClassCDevice(HttpRequest req, ILogger log)
        {
            var devEUI = req.Query["DevEUI"];
            if (StringValues.IsNullOrEmpty(devEUI))
            {
                log.LogError("DevEUI missing in request");
                return new BadRequestObjectResult("DevEUI missing in request");
            }
            try
            {
                EUIValidator.ValidateDevEUI(devEUI);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            var fPort = req.Query["FPort"];
            if (StringValues.IsNullOrEmpty(fPort))
            {
                log.LogError("FPort missing in request");
                return new BadRequestObjectResult("FPort missing in request");
            }

            var payload = req.Query["RawPayload"];
            if (StringValues.IsNullOrEmpty(payload))
            {
                log.LogError("RawPayload missing in request");
                return new BadRequestObjectResult("RawPayload missing in request");
            }
        }
    }
}
