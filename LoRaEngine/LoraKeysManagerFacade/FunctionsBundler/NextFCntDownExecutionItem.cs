// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal class NextFCntDownExecutionItem : IFunctionBundlerExecutionItem
    {
        public Task<FunctionBundlerExecutionState> Execute(IPipelineExecutionContext context)
        {
            if (!context.Result.NextFCntDown.HasValue)
            {
                var next = FCntCacheCheck.GetNextFCntDown(context.DevEUI, context.Request.GatewayId, context.Request.ClientFCntUp, context.Request.ClientFCntDown, context.FunctionAppDirectory);
                context.Result.NextFCntDown = next > 0 ? next : (int?)null;
            }

            return Task.FromResult(FunctionBundlerExecutionState.Continue);
        }

        public bool NeedsToExecute(FunctionBundlerItem item)
        {
            return (item & FunctionBundlerItem.FCntDown) == FunctionBundlerItem.FCntDown;
        }

        public Task OnAbortExecution(IPipelineExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
