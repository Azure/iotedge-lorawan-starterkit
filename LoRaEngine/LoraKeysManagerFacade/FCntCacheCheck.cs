// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public class FCntCacheCheck
    {
        private readonly ILoRaDeviceCacheStore deviceCache;

        public FCntCacheCheck(ILoRaDeviceCacheStore deviceCache)
        {
            this.deviceCache = deviceCache;
        }

        [FunctionName("NextFCntDown")]
        public async Task<IActionResult> NextFCntDownInvoke(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            _ = req ?? throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            var devEUI = req.Query["DevEUI"];
            var fCntDown = req.Query["FCntDown"];
            var fCntUp = req.Query["FCntUp"];
            var gatewayId = req.Query["GatewayId"];
            var abpFcntCacheReset = req.Query["ABPFcntCacheReset"];

            EUIValidator.ValidateDevEUI(devEUI);

            if (!uint.TryParse(fCntUp, out var clientFCntUp))
            {
                throw new ArgumentException("Missing FCntUp");
            }

            if (abpFcntCacheReset != StringValues.Empty)
            {
                using (var deviceCache = new LoRaDeviceCache(this.deviceCache, devEUI, gatewayId))
                {
                    if (await deviceCache.TryToLockAsync())
                    {
                        if (deviceCache.TryGetInfo(out var deviceInfo))
                        {
                            // only reset the cache if the current value is larger
                            // than 1 otherwise we likely reset it from another device
                            // and continued processing
                            if (deviceInfo.FCntUp > 1)
                            {
                                log.LogDebug("Resetting cache for device {devEUI}. FCntUp: {fcntup}", devEUI, deviceInfo.FCntUp);
                                deviceCache.ClearCache();
                            }
                        }
                    }
                }

                return new OkObjectResult(null);
            }

            // validate input parameters
            if (!uint.TryParse(fCntDown, out var clientFCntDown) ||
                StringValues.IsNullOrEmpty(gatewayId))
            {
                var errorMsg = "Missing FCntDown or GatewayId";
                throw new ArgumentException(errorMsg);
            }

            var newFCntDown = await this.GetNextFCntDownAsync(devEUI, gatewayId, clientFCntUp, clientFCntDown);

            return new OkObjectResult(newFCntDown);
        }

        public async Task<uint> GetNextFCntDownAsync(string devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            uint newFCntDown = 0;
            using (var deviceCache = new LoRaDeviceCache(this.deviceCache, devEUI, gatewayId))
            {
                if (await deviceCache.TryToLockAsync())
                {
                    if (deviceCache.TryGetInfo(out var serverStateForDeviceInfo))
                    {
                        newFCntDown = ProcessExistingDeviceInfo(deviceCache, serverStateForDeviceInfo, gatewayId, clientFCntUp, clientFCntDown);
                    }
                    else
                    {
                        newFCntDown = clientFCntDown + 1;
                        var state = deviceCache.Initialize(clientFCntUp, newFCntDown);
                    }
                }
            }

            return newFCntDown;
        }

        internal static uint ProcessExistingDeviceInfo(LoRaDeviceCache deviceCache, DeviceCacheInfo cachedDeviceState, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            uint newFCntDown = 0;

            if (cachedDeviceState != null)
            {
                // we have a state in the cache matching this device and now we own the lock
                if (clientFCntUp > cachedDeviceState.FCntUp)
                {
                    // it is a new message coming up by the first gateway
                    if (clientFCntDown >= cachedDeviceState.FCntDown)
                        newFCntDown = clientFCntDown + 1;
                    else
                        newFCntDown = cachedDeviceState.FCntDown + 1;

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
