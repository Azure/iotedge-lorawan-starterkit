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

    public class DuplicateMsgCacheCheck
    {
        const string QueryParamDevEUI = "DevEUI";
        const string QueryParamGatewayId = "GatewayId";
        const string QueryParamFCntUp = "FCntUp";
        const string QueryParamFCntDown = "FCntDown";
        const string QueryParamCacheReset = "CacheReset";

        private readonly ILoRaDeviceCacheStore cacheStore;

        public DuplicateMsgCacheCheck(ILoRaDeviceCacheStore cacheStore)
        {
            this.cacheStore = cacheStore;
        }

        [FunctionName("DuplicateMsgCheck")]
        public IActionResult DuplicateMsgCheck(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "DuplicateMsgCheck/{devEUI}")]HttpRequest req,
            ILogger log,
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
                this.cacheStore.KeyDelete(devEUI);
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

            var result = this.GetDuplicateMessageResult(devEUI, gatewayId, clientFCntUp, clientFCntDown);

            return new OkObjectResult(result);
        }

        public DuplicateMsgResult GetDuplicateMessageResult(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            var isDuplicate = true;
            string processedDevice = gatewayId;

            using (var deviceCache = new LoRaDeviceCache(this.cacheStore, devEUI, gatewayId))
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
