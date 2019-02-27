// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal class NextFCntDownExecutionItem : IFunctionBundlerExecutionItem
    {
        public Task<FunctionBundlerExecutionState> Execute(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory)
        {
            if (!result.NextFCntDown.HasValue)
            {
                var next = FCntCacheCheck.GetNextFCntDown(devEUI, request.GatewayId, request.ClientFCntUp, request.ClientFCntDown, functionAppDirectory);
                result.NextFCntDown = next > 0 ? next : (int?)null;
            }

            return Task.FromResult(FunctionBundlerExecutionState.Continue);
        }

        public bool NeedsToExecute(FunctionBundlerItem item)
        {
            return (item & FunctionBundlerItem.FCntDown) == FunctionBundlerItem.FCntDown;
        }

        public Task OnAbortExecution(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory)
        {
            return Task.CompletedTask;
        }
    }
}
