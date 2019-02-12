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

    public static class DuplicateMsgCacheCheck
    {
        const string QueryParamDevEUI = "DevEUI";
        const string QueryParamGatewayId = "GatewayId";
        const string QueryParamFCntUp = "FCntUp";
        const string QueryParamFCntDown = "FCntDown";
        const string QueryParamCacheReset = "CacheReset";

        [FunctionName(nameof(DuplicateMsgCheck))]
        public static IActionResult DuplicateMsgCheck([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            var currentApiVersion = ApiVersion.LatestVersion;
            req.HttpContext.Response.Headers.Add(ApiVersion.HttpHeaderName, currentApiVersion.Version);

            var requestedVersion = req.GetRequestedVersion();
            if (requestedVersion == null || !currentApiVersion.SupportsVersion(requestedVersion))
            {
                return new BadRequestObjectResult($"Incompatible versions (requested: '{requestedVersion.Name ?? string.Empty}', current: '{currentApiVersion.Name}')");
            }

            string cacheReset = req.Query[QueryParamCacheReset];
            string devEUI = req.Query[QueryParamDevEUI];

            if (!string.IsNullOrEmpty(cacheReset) && !string.IsNullOrEmpty(devEUI))
            {
                LoRaDeviceCache.Delete(devEUI, context);
                return (ActionResult)new OkObjectResult(null);
            }

            string gatewayId = req.Query[QueryParamGatewayId];
            string fCntDown = req.Query[QueryParamFCntDown];
            string fCntUp = req.Query[QueryParamFCntUp];

            if (string.IsNullOrEmpty(devEUI) ||
                string.IsNullOrEmpty(gatewayId) ||
                !int.TryParse(fCntUp, out int clientFCntUp))
            {
                string errorMsg = $"Missing {QueryParamDevEUI} or {QueryParamFCntUp} or {QueryParamGatewayId}";
                throw new Exception(errorMsg);
            }

            var isDuplicate = true;
            string processedDevice = gatewayId;
            int? newClientFCntDown = null;
            int clientFCntDown = 0;

            using (var deviceCache = LoRaDeviceCache.Create(context, devEUI, gatewayId))
            {
                if (deviceCache.TryToLock())
                {
                    // we are owning the lock now
                    if (deviceCache.TryGetInfo(out DeviceCacheInfo cachedDeviceState))
                    {
                        var updateCacheState = false;

                        if (cachedDeviceState.FCntUp < clientFCntUp)
                        {
                            isDuplicate = false;
                            updateCacheState = true;
                        }
                        else if (cachedDeviceState.FCntUp == clientFCntUp && cachedDeviceState.GatewayId == gatewayId)
                        {
                            isDuplicate = false;
                            processedDevice = cachedDeviceState.GatewayId;
                        }
                        else
                        {
                            processedDevice = cachedDeviceState.GatewayId;
                        }

                        if (!isDuplicate && int.TryParse(fCntDown, out clientFCntDown))
                        {
                            // requires a down confirmation
                            // combine the logic from FCntCacheCheck to avoid 2 roundtrips
                            newClientFCntDown = FCntCacheCheck.ProcessExistingDeviceInfo(deviceCache, cachedDeviceState, gatewayId, clientFCntUp, clientFCntDown);
                        }
                        else if (updateCacheState)
                        {
                            cachedDeviceState.FCntUp = clientFCntUp;
                            cachedDeviceState.GatewayId = gatewayId;
                            deviceCache.StoreInfo(cachedDeviceState);
                        }
                    }
                    else
                    {
                        // initialize
                        int.TryParse(fCntDown, out clientFCntDown);
                        isDuplicate = false;
                        var state = deviceCache.Initialize(clientFCntDown, clientFCntUp);
                        newClientFCntDown = state.FCntDown;
                    }
                }
            }

            return (ActionResult)new OkObjectResult(new
            {
                IsDuplicate = isDuplicate,
                GatewayId = processedDevice,
                ClientFCntDown = newClientFCntDown
            });
        }
    }
}
