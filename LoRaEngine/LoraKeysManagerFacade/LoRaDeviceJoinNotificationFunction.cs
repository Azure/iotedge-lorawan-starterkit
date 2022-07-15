// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using LoRaTools;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    internal class LoRaDeviceJoinNotificationFunction
    {
        private readonly LoRaDevAddrCache loRaDevAddrCache;
        private readonly ILogger<LoRaDeviceJoinNotificationFunction> logger;

        public LoRaDeviceJoinNotificationFunction(LoRaDevAddrCache loRaDevAddrCache, ILogger<LoRaDeviceJoinNotificationFunction> logger)
        {
            this.loRaDevAddrCache = loRaDevAddrCache;
            this.logger = logger;
        }

        [FunctionName("DeviceJoinNotification")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "devicejoinnotification")] DeviceJoinNotification joinNotification,
                                 HttpRequest req)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            using var deviceScope = this.logger.BeginDeviceScope(joinNotification.DevEUI);

            this.loRaDevAddrCache.StoreInfo(new DevAddrCacheInfo
            {
                DevAddr = joinNotification.DevAddr,
                DevEUI = joinNotification.DevEUI,
                GatewayId = joinNotification.GatewayId,
                NwkSKey = joinNotification.NwkSKeyString
            });

            return new OkResult();
        }
    }
}
