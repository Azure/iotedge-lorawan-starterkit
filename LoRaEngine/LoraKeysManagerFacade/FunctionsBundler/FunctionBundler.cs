// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public static class FunctionBundler
    {
        [FunctionName("FunctionBundler")]
        public static async Task<IActionResult> FunctionBundlerImpl(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "FunctionBundler/{devEUI}")]HttpRequest req,
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

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var functionBundlerRequest = JsonConvert.DeserializeObject<FunctionBundlerRequest>(requestBody);
            var result = await HandleFunctionBundlerInvoke(devEUI, functionBundlerRequest, context.FunctionAppDirectory);

            return new OkObjectResult(result);
        }

        public static async Task<FunctionBundlerResult> HandleFunctionBundlerInvoke(string devEUI, FunctionBundlerRequest request, string functionAppDirectory)
        {
            var pipeline = new FunctionBundlerPipelineExecuter(devEUI, request, functionAppDirectory);
            return await pipeline.Execute();
        }
    }
}
