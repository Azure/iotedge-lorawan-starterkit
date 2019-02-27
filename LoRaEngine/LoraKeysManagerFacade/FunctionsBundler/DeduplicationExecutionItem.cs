// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal class DeduplicationExecutionItem : IFunctionBundlerExecutionItem
    {
        public Task<FunctionBundlerExecutionState> Execute(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory)
        {
            result.DeduplicationResult = DuplicateMsgCacheCheck.GetDuplicateMessageResult(devEUI, request.GatewayId, request.ClientFCntUp, request.ClientFCntDown, functionAppDirectory);
            return Task.FromResult(result.DeduplicationResult.IsDuplicate ? FunctionBundlerExecutionState.Abort : FunctionBundlerExecutionState.Continue);
        }

        public bool NeedsToExecute(FunctionBundlerItem item)
        {
            return (item & FunctionBundlerItem.Deduplication) == FunctionBundlerItem.Deduplication;
        }

        public Task OnAbortExecution(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory)
        {
            return Task.CompletedTask;
        }
    }
}
