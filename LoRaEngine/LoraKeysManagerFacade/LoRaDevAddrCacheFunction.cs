// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    internal class LoRaDevAddrCacheFunction
    {
        private readonly LoRaDevAddrCache loRaDevAddrCache;
        private readonly ILogger<LoRaDevAddrCacheFunction> logger;

        public LoRaDevAddrCacheFunction(LoRaDevAddrCache loRaDevAddrCache, ILogger<LoRaDevAddrCacheFunction> logger)
        {
            this.loRaDevAddrCache = loRaDevAddrCache;
            this.logger = logger;
        }

        [FunctionName("StoreInDevAddrCache")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "storeindevaddrcache")] HttpRequest req)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var devAddrCacheInfo = JsonConvert.DeserializeObject<DevAddrCacheInfo>(requestBody);
            using var deviceScope = this.logger.BeginDeviceScope(devAddrCacheInfo.DevEUI);

            this.loRaDevAddrCache.StoreInfo(devAddrCacheInfo);

            return new OkResult();
        }
    }
}
