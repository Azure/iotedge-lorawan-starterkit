// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.Shared;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Azure.WebJobs.Host;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public static class FCntCacheCheck
    {
        [FunctionName(nameof(NextFCntDown))]
        public static IActionResult NextFCntDown([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            return Run(req, log, context, ApiVersion.LatestVersion);
        }

        public static IActionResult Run(HttpRequest req, ILogger log, ExecutionContext context, ApiVersion currentApiVersion)
        {
            // set the current version in the response header
            req.HttpContext.Response.Headers.Add(ApiVersion.HttpHeaderName, currentApiVersion.Version);

            var requestedVersion = req.GetRequestedVersion();
            if (requestedVersion == null || !currentApiVersion.SupportsVersion(requestedVersion))
            {
                return new BadRequestObjectResult($"Incompatible versions (requested: '{requestedVersion.Name ?? string.Empty}', current: '{currentApiVersion.Name}')");
            }

            string devEUI = req.Query["DevEUI"];
            string fCntDown = req.Query["FCntDown"];
            string fCntUp = req.Query["FCntUp"];
            string gatewayId = req.Query["GatewayId"];
            string abpFcntCacheReset = req.Query["ABPFcntCacheReset"];
            int newFCntDown = 0;

            if (!string.IsNullOrEmpty(abpFcntCacheReset))
            {
                LoRaDeviceCache.Delete(devEUI, context);
                return (ActionResult)new OkObjectResult(null);
            }

            // validate input parameters
            if (!int.TryParse(fCntDown, out int clientFCntDown) ||
                !int.TryParse(fCntUp, out int clientFCntUp) ||
                string.IsNullOrEmpty(devEUI) ||
                string.IsNullOrEmpty(gatewayId))
            {
                string errorMsg = "Missing DevEUI or FCntDown or FCntUp or GatewayId";
                throw new ArgumentException(errorMsg);
            }

            using (var deviceCache = LoRaDeviceCache.Create(context, devEUI, gatewayId))
            {
                if (deviceCache.TryToLock())
                {
                    if (deviceCache.TryGetInfo(out DeviceCacheInfo serverStateForDeviceInfo))
                    {
                        newFCntDown = ProcessExistingDeviceInfo(deviceCache, serverStateForDeviceInfo, gatewayId, clientFCntUp, clientFCntDown);
                    }
                    else
                    {
                        newFCntDown = clientFCntDown + 1;
                        deviceCache.Initialize(newFCntDown, clientFCntUp);
                    }
                }
            }

            return (ActionResult)new OkObjectResult(newFCntDown);
        }

        internal static int ProcessExistingDeviceInfo(LoRaDeviceCache deviceCache, DeviceCacheInfo cachedDeviceState, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            int newFCntDown = 0;

            if (cachedDeviceState != null)
            {
                // we have a state in the cache matching this device and now we own the lock
                if (clientFCntUp > cachedDeviceState.FCntUp)
                {
                    // it is a new message coming up by the first gateway
                    if (clientFCntDown >= cachedDeviceState.FCntDown)
                        newFCntDown = (int)(clientFCntDown + 1);
                    else
                        newFCntDown = (int)(cachedDeviceState.FCntDown + 1);

                    cachedDeviceState.FCntUp = clientFCntUp;
                    cachedDeviceState.FCntDown = newFCntDown;
                    cachedDeviceState.GatewayId = gatewayId;

                    deviceCache.StoreInfo(cachedDeviceState);
                }
                else if (clientFCntUp == cachedDeviceState.FCntUp && gatewayId == cachedDeviceState.GatewayId)
                {
                    // it is a retry message coming up by the same first gateway
                    newFCntDown = cachedDeviceState.FCntDown + 1;
                    cachedDeviceState.FCntDown = newFCntDown;

                    deviceCache.StoreInfo(cachedDeviceState);
                }
            }

            return newFCntDown;
        }
    }
}
