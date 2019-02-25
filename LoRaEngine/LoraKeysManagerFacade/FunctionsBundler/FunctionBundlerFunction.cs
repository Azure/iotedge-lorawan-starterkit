// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class FunctionBundlerFunction
    {
        private FunctionBundlerContext context;

        public FunctionBundlerFunction(FunctionBundlerContext context)
        {
            this.context = context;
        }

        [FunctionName("FunctionBundler")]
        public async Task<IActionResult> FunctionBundlerImpl(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "FunctionBundler/{devEUI}")]HttpRequest req,
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

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var functionBundlerRequest = JsonConvert.DeserializeObject<FunctionBundlerRequest>(requestBody);
            var result = await this.HandleFunctionBundlerInvoke(devEUI, functionBundlerRequest);

            return new OkObjectResult(result);
        }

        public async Task<FunctionBundlerResult> HandleFunctionBundlerInvoke(string devEUI, FunctionBundlerRequest request)
        {
            var pipeline = new FunctionBundlerPipelineExecuter(devEUI, request, this.context);
            return await pipeline.Execute();
        }
    }
}
