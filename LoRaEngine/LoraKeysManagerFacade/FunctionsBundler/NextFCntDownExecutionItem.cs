// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    public class NextFCntDownExecutionItem : IFunctionBundlerExecutionItem
    {
        private readonly FCntCacheCheck fCntCacheCheck;

        public NextFCntDownExecutionItem(FCntCacheCheck fCntCacheCheck)
        {
            this.fCntCacheCheck = fCntCacheCheck;
        }

        public Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            if (!context.Result.NextFCntDown.HasValue)
            {
                var next = this.fCntCacheCheck.GetNextFCntDown(context.DevEUI, context.Request.GatewayId, context.Request.ClientFCntUp, context.Request.ClientFCntDown);
                context.Result.NextFCntDown = next > 0 ? next : (uint?)null;
            }

            return Task.FromResult(FunctionBundlerExecutionState.Continue);
        }

        public int Priority => 3;

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.FCntDown) == FunctionBundlerItemType.FCntDown;
        }

        public Task OnAbortExecutionAsync(IPipelineExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
