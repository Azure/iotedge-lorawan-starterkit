// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using Microsoft.Extensions.Logging;

    public class FunctionBundlerDeduplicationExecutionItem : IFunctionBundlerExecutionItem
    {
        public void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request)
        {
            request.FunctionItems |= FunctionBundlerItemType.Deduplication;
        }

        public bool RequiresExecution(FunctionBundlerExecutionContext context)
        {
            return context.DeduplicationFactory.Create(context.LoRaDevice) != null;
        }

        public void ProcessResult(FunctionBundlerExecutionContext context, FunctionBundlerResult result)
        {
            if (result.DeduplicationResult != null)
            {
                var strategy = context.DeduplicationFactory.Create(context.LoRaDevice);
                if (strategy != null)
                {
                    result.DeduplicationResult = strategy.Process(result.DeduplicationResult, context.FCntUp);
                }
            }
        }
    }
}
