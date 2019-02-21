// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
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
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "FunctionBundler/{devEUI}")]HttpRequest req,
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

            var functionBundlerRequest = JsonConvert.DeserializeObject<FunctionBundlerRequest>(requestBody);
            var result = await HandleFunctionBundlerInvoke(devEUI, functionBundlerRequest, context);

            return new OkObjectResult(result);
        }

        internal static async Task<FunctionBundlerResult> HandleFunctionBundlerInvoke(string devEUI, FunctionBundlerRequest request, ExecutionContext context)
        {
            if (request == null)
            {
                return null;
            }

            var result = new FunctionBundlerResult();

            var performADR = (request.FunctionItems & FunctionBundlerItem.ADR) == FunctionBundlerItem.ADR;

            if ((request.FunctionItems & FunctionBundlerItem.Deduplication) == FunctionBundlerItem.Deduplication)
            {
                result.DeduplicationResult = DuplicateMsgCacheCheck.GetDuplicateMessageResult(devEUI, request.GatewayId, request.ClientFCntUp, null, context);
            }

            if (result.DeduplicationResult != null && result.DeduplicationResult.IsDuplicate)
            {
                // even if this is a duplication, we want to record ADR info if it was requested
                if (performADR && request.AdrRequest != null)
                {
                    request.AdrRequest.PerformADRCalculation = false; // we lost the race, no calculation
                    result.AdrResult = await LoRaADRFunction.HandleADRRequest(devEUI, request.AdrRequest, context);
                }
            }
            else if (performADR)
            {
                result.AdrResult = await LoRaADRFunction.HandleADRRequest(devEUI, request.AdrRequest, context);
                result.NextFCntDown = result.AdrResult.FCntDown;
            }
            else if (result.NextFCntDown == 0 && (request.FunctionItems & FunctionBundlerItem.FCntDown) == FunctionBundlerItem.FCntDown)
            {
                result.NextFCntDown = FCntCacheCheck.GetNextFCntDown(devEUI, request.GatewayId, request.ClientFCntUp, request.ClientFCntDown, context);
            }

            return result;
        }
    }
}
