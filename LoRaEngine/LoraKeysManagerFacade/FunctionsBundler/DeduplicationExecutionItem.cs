// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal class DeduplicationExecutionItem : IFunctionBundlerExecutionItem
    {
        public Task<FunctionBundlerExecutionState> Execute(IPipelineExecutionContext context)
        {
            context.Result.DeduplicationResult = DuplicateMsgCacheCheck.GetDuplicateMessageResult(context.DevEUI, context.Request.GatewayId, context.Request.ClientFCntUp, context.Request.ClientFCntDown, context.FunctionAppDirectory);
            return Task.FromResult(context.Result.DeduplicationResult.IsDuplicate ? FunctionBundlerExecutionState.Abort : FunctionBundlerExecutionState.Continue);
        }

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.Deduplication) == FunctionBundlerItemType.Deduplication;
        }

        public Task OnAbortExecution(IPipelineExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
