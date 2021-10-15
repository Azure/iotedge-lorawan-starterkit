// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using System;

    public class FunctionBundlerFCntDownExecutionItem : IFunctionBundlerExecutionItem
    {
        public void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            request.FunctionItems |= FunctionBundlerItemType.FCntDown;
        }

        public bool RequiresExecution(FunctionBundlerExecutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            return context.LoRaPayload.IsConfirmed || context.LoRaPayload.IsMacAnswerRequired;
        }

        public void ProcessResult(FunctionBundlerExecutionContext context, FunctionBundlerResult result)
        {
        }
    }
}
