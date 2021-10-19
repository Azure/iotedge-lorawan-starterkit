// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using System;

    public class FunctionBundlerDeduplicationExecutionItem : IFunctionBundlerExecutionItem
    {
        public void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            request.FunctionItems |= FunctionBundlerItemType.Deduplication;
        }

        public bool RequiresExecution(FunctionBundlerExecutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            return context.DeduplicationFactory.Create(context.LoRaDevice) != null;
        }

        public void ProcessResult(FunctionBundlerExecutionContext context, FunctionBundlerResult result)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (result is null) throw new ArgumentNullException(nameof(result));

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
