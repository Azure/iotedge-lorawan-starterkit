// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal class ADRExecutionItem : IFunctionBundlerExecutionItem
    {
        public async Task<FunctionBundlerExecutionState> Execute(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory)
        {
            result.AdrResult = await LoRaADRFunction.HandleADRRequest(devEUI, request.AdrRequest, functionAppDirectory);
            result.NextFCntDown = result?.AdrResult.FCntDown > 0 ? result.AdrResult.FCntDown : (int?)null;
            return FunctionBundlerExecutionState.Continue;
        }

        public bool NeedsToExecute(FunctionBundlerItem item)
        {
            return (item & FunctionBundlerItem.ADR) == FunctionBundlerItem.ADR;
        }

        public async Task OnAbortExecution(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory)
        {
            // aborts of the full pipeline indicate we do not calculate but we still want to capture the frame
            // if we have one
            request.AdrRequest.PerformADRCalculation = false;
            result.AdrResult = await LoRaADRFunction.HandleADRRequest(devEUI, request.AdrRequest, functionAppDirectory);
        }
    }
}
