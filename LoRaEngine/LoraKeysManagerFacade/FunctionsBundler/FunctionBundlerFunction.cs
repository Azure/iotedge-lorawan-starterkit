// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class FunctionBundlerFunction
    {
        private readonly IFunctionBundlerExecutionItem[] executionItems;
        private readonly ILogger<FunctionBundlerFunction> logger;

        public FunctionBundlerFunction(
            IFunctionBundlerExecutionItem[] items, ILogger<FunctionBundlerFunction> logger)
        {
            this.executionItems = items.OrderBy(x => x.Priority).ToArray();
            this.logger = logger;
        }

        [FunctionName("FunctionBundler")]
        public async Task<IActionResult> FunctionBundler(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "FunctionBundler/{devEUI}")] HttpRequest req,
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

            if (!DevEui.TryParse(devEUI, EuiParseOptions.ForbidInvalid, out var parsedDevEui))
            {
                return new BadRequestObjectResult("Dev EUI is invalid.");
            }

            using var deviceScope = this.logger.BeginDeviceScope(parsedDevEui);

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var functionBundlerRequest = JsonConvert.DeserializeObject<FunctionBundlerRequest>(requestBody);
            var result = await HandleFunctionBundlerInvoke(parsedDevEui, functionBundlerRequest);

            return new OkObjectResult(result);
        }

        public async Task<FunctionBundlerResult> HandleFunctionBundlerInvoke(DevEui devEUI, FunctionBundlerRequest request)
        {
            var pipeline = new FunctionBundlerPipelineExecuter(this.executionItems, devEUI, request, this.logger);
            return await pipeline.Execute();
        }
    }
}
