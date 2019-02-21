// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using LoRaWan.Shared;
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

        [FunctionName(nameof(DuplicateMsgCheck))]
        public static IActionResult DuplicateMsgCheck([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex);
            }

            string cacheReset = req.Query[QueryParamCacheReset];
            string devEUI = req.Query[QueryParamDevEUI];

            EUIValidator.ValidateDevEUI(devEUI);

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

            int? clientFCntDown = null;
            if (int.TryParse(fCntDown, out var down))
            {
                clientFCntDown = down;
            }

            var result = GetDuplicateMessageResult(devEUI, gatewayId, clientFCntUp, clientFCntDown, context);

            return new OkObjectResult(result);
        }

        public static DuplicateMsgResult GetDuplicateMessageResult(string devEUI, string gatewayId, int clientFCntUp, int? clientFCntDown, ExecutionContext context)
        {
            var isDuplicate = true;
            string processedDevice = gatewayId;
            int? newClientFCntDown = null;

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

                        if (!isDuplicate && clientFCntDown.HasValue)
                        {
                            // requires a down confirmation
                            // combine the logic from FCntCacheCheck to avoid 2 roundtrips
                            newClientFCntDown = FCntCacheCheck.ProcessExistingDeviceInfo(deviceCache, cachedDeviceState, gatewayId, clientFCntUp, clientFCntDown.Value);
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
                        isDuplicate = false;
                        var state = deviceCache.Initialize(clientFCntDown.GetValueOrDefault(), clientFCntUp);
                        if (clientFCntDown.HasValue)
                        {
                            newClientFCntDown = state.FCntDown;
                        }
                    }
                }
            }

            return new DuplicateMsgResult
            {
                IsDuplicate = isDuplicate,
                GatewayId = processedDevice,
                ClientFCntDown = newClientFCntDown
            };
        }
    }
}
