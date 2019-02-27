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
            if (request == null)
            {
                return null;
            }

            var result = new FunctionBundlerResult();
            int? nextFrameCountDown = null;

            var performADR = (request.FunctionItems & FunctionBundlerItem.ADR) == FunctionBundlerItem.ADR;

            if ((request.FunctionItems & FunctionBundlerItem.Deduplication) == FunctionBundlerItem.Deduplication)
            {
                result.DeduplicationResult = DuplicateMsgCacheCheck.GetDuplicateMessageResult(devEUI, request.GatewayId, request.ClientFCntUp, request.ClientFCntDown, functionAppDirectory);
            }

            if (result.DeduplicationResult != null && result.DeduplicationResult.IsDuplicate)
            {
                // even if this is a duplication, we want to record ADR info if it was requested
                if (performADR && request.AdrRequest != null)
                {
                    request.AdrRequest.PerformADRCalculation = false; // we lost the race, no calculation
                    result.AdrResult = await LoRaADRFunction.HandleADRRequest(devEUI, request.AdrRequest, functionAppDirectory);
                }
            }
            else
            {
                if (performADR)
                {
                    result.AdrResult = await LoRaADRFunction.HandleADRRequest(devEUI, request.AdrRequest, functionAppDirectory);
                    nextFrameCountDown = result?.AdrResult.FCntDown > 0 ? result.AdrResult.FCntDown : (int?)null;
                }

                if (nextFrameCountDown == null && (request.FunctionItems & FunctionBundlerItem.FCntDown) == FunctionBundlerItem.FCntDown)
                {
                    var next = FCntCacheCheck.GetNextFCntDown(devEUI, request.GatewayId, request.ClientFCntUp, request.ClientFCntDown, functionAppDirectory);
                    nextFrameCountDown = next > 0 ? next : (int?)null;
                }
            }

            result.NextFCntDown = nextFrameCountDown;
            return result;
        }
    }
}
