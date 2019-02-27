// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    public static class DuplicateMsgCacheCheck
    {
        const string QueryParamDevEUI = "DevEUI";
        const string QueryParamGatewayId = "GatewayId";
        const string QueryParamFCntUp = "FCntUp";
        const string QueryParamFCntDown = "FCntDown";
        const string QueryParamCacheReset = "CacheReset";

        [FunctionName("DuplicateMsgCheck")]
        public static IActionResult DuplicateMsgCheck(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "DuplicateMsgCheck/{devEUI}")]HttpRequest req,
            ILogger log,
            ExecutionContext context,
            string devEUI)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            EUIValidator.ValidateDevEUI(devEUI);

            var cacheReset = req.Query[QueryParamCacheReset];
            if (!string.IsNullOrEmpty(cacheReset) && !string.IsNullOrEmpty(devEUI))
            {
                LoRaDeviceCache.Delete(devEUI, context.FunctionAppDirectory);
                return (ActionResult)new OkObjectResult(null);
            }

            var gatewayId = req.Query[QueryParamGatewayId];
            var fCntDown = req.Query[QueryParamFCntDown];
            var fCntUp = req.Query[QueryParamFCntUp];

            if (string.IsNullOrEmpty(devEUI) ||
                string.IsNullOrEmpty(gatewayId) ||
                !int.TryParse(fCntUp, out int clientFCntUp) ||
                !int.TryParse(fCntDown, out int clientFCntDown))
            {
                var errorMsg = $"Missing {QueryParamDevEUI} or {QueryParamFCntUp} or {QueryParamGatewayId}";
                throw new Exception(errorMsg);
            }

            var result = GetDuplicateMessageResult(devEUI, gatewayId, clientFCntUp, clientFCntDown, context.FunctionAppDirectory);

            return new OkObjectResult(result);
        }

        public static DuplicateMsgResult GetDuplicateMessageResult(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown, string functionAppDirectory)
        {
            var isDuplicate = true;
            string processedDevice = gatewayId;

            using (var deviceCache = LoRaDeviceCache.Create(functionAppDirectory, devEUI, gatewayId))
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

                        if (updateCacheState)
                        {
                            cachedDeviceState.FCntUp = clientFCntUp;
                            cachedDeviceState.GatewayId = gatewayId;
                            deviceCache.StoreInfo(cachedDeviceState);
                        }
                    }
                    else
                    {
                        // initialize
                        isDuplicate = false;
                        var state = deviceCache.Initialize(clientFCntUp, clientFCntDown);
                    }
                }
            }

            return new DuplicateMsgResult
            {
                IsDuplicate = isDuplicate,
                GatewayId = processedDevice
            };
        }
    }
}
