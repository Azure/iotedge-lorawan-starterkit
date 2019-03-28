// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        private readonly IFunctionBundlerExecutionItem[] executionItems;

        public FunctionBundlerFunction(
            IFunctionBundlerExecutionItem[] items)
        {
            this.executionItems = items.OrderBy(x => x.Priority).ToArray();
        }

        [FunctionName("FunctionBundler")]
        public async Task<IActionResult> FunctionBundlerImpl(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "FunctionBundler/{devEUI}")]HttpRequest req,
            ILogger logger,
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
            var result = await this.HandleFunctionBundlerInvoke(devEUI, functionBundlerRequest, logger);

            return new OkObjectResult(result);
        }

        public async Task<FunctionBundlerResult> HandleFunctionBundlerInvoke(string devEUI, FunctionBundlerRequest request, ILogger logger = null)
        {
            var pipeline = new FunctionBundlerPipelineExecuter(this.executionItems, devEUI, request, logger);
            return await pipeline.Execute();
        }
    }
}
