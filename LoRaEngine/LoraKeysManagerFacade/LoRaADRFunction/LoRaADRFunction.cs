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
        private static ILoRaADRManager adrManager;
        private static object adrManagerSyncLock = new object();

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

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var adrRequest = JsonConvert.DeserializeObject<LoRaADRRequest>(requestBody);
            var result = await HandleADRRequest(adrRequest, EnsureLoraADRManagerInstance(context.FunctionAppDirectory));

            return (ActionResult)new OkResult();
        }

        internal static async Task<LoRaADRResult> HandleADRRequest(LoRaADRRequest request, ILoRaADRManager adrManager)
        {
            return await Task.FromResult<LoRaADRResult>(null);
        }

        private static ILoRaADRManager EnsureLoraADRManagerInstance(string functionAppDirectory)
        {
            if (adrManager != null)
            {
                return adrManager;
            }

            lock (adrManagerSyncLock)
            {
                if (adrManager == null)
                {
                    var redisStore = new LoRaADRRedisStore(FunctionConfigManager.GetCurrentConfiguration(functionAppDirectory).GetConnectionString("RedisConnectionString"));
                    adrManager = new LoRaLocalADRManager(redisStore, new LoRaADRStrategyProvider());
                }
            }

            return adrManager;
        }
    }
}
