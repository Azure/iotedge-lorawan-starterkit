// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal class ADRExecutionItem : IFunctionBundlerExecutionItem
    {
        public async Task<FunctionBundlerExecutionState> Execute(IPipelineExecutionContext context)
        {
            var adrFunction = context.FunctionContext.LoRaADRFunction;
            context.Result.AdrResult = await adrFunction.HandleADRRequest(context.DevEUI, context.Request.AdrRequest);
            context.Result.NextFCntDown = context.Result?.AdrResult.FCntDown > 0 ? context.Result.AdrResult.FCntDown : (uint?)null;
            return FunctionBundlerExecutionState.Continue;
        }

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.ADR) == FunctionBundlerItemType.ADR;
        }

        public async Task OnAbortExecution(IPipelineExecutionContext context)
        {
            // aborts of the full pipeline indicate we do not calculate but we still want to capture the frame
            // if we have one
            var adrFunction = context.FunctionContext.LoRaADRFunction;
            context.Request.AdrRequest.PerformADRCalculation = false;
            context.Result.AdrResult = await adrFunction.HandleADRRequest(context.DevEUI, context.Request.AdrRequest);
        }
    }
}
