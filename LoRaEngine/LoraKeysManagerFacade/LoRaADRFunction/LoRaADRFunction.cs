// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public static class LoRaADRFunction
    {
        private static readonly object AdrManagerSyncLock = new object();
        private static ILoRaADRManager adrManager;

        [FunctionName("ADRFunction")]
        public static async Task<IActionResult> FunctionBundlerImpl(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "ADRFunction/{devEUI}")]HttpRequest req,
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
                return new BadRequestObjectResult(ex);
            }

            EUIValidator.ValidateDevEUI(devEUI);

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var adrRequest = JsonConvert.DeserializeObject<LoRaADRRequest>(requestBody);
            var result = await HandleADRRequest(devEUI, adrRequest, context);

            return new OkObjectResult(result);
        }

        internal static async Task<LoRaADRResult> HandleADRRequest(string devEUI, LoRaADRRequest request, ExecutionContext context)
        {
            var adrManager = EnsureLoraADRManagerInstance(context.FunctionAppDirectory);
            var newEntry = new LoRaADRTableEntry
            {
                 DevEUI = devEUI,
                 FCnt = request.FCntUp,
                 GatewayId = request.GatewayId,
                 Snr = request.RequiredSnr
            };

            if (request.PerformADRCalculation)
            {
                var adrResult = await adrManager.CalculateADRResultAndAddEntry(devEUI, request.GatewayId, request.FCntUp, request.FCntDown, request.RequiredSnr, request.DataRate, request.MinTxPowerIndex, newEntry);
                if (adrResult == null)
                {
                    adrResult = await adrManager.GetLastResult(devEUI);
                }

                return adrResult;
            }
            else
            {
                await adrManager.StoreADREntry(newEntry);
            }

            return null;
        }

        private static ILoRaADRManager EnsureLoraADRManagerInstance(string functionAppDirectory)
        {
            if (adrManager != null)
            {
                return adrManager;
            }

            lock (AdrManagerSyncLock)
            {
                if (adrManager == null)
                {
                    var redisStore = new LoRaADRRedisStore(FunctionConfigManager.GetCurrentConfiguration(functionAppDirectory).GetConnectionString("RedisConnectionString"));
                    adrManager = new LoRaADRServerManager(redisStore, new LoRaADRStrategyProvider(), functionAppDirectory);
                    // adrManager = new LoRaADRDefaultManager(new LoRaADRInMemoryStore(), new LoRaADRStrategyProvider());
                }
            }

            return adrManager;
        }
    }
}
