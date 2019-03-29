// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;

    public interface IFunctionBundlerExecutionItem
    {
        bool RequiresExecution(FunctionBundlerExecutionContext context);

        void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request);

        void ProcessResult(FunctionBundlerExecutionContext context, FunctionBundlerResult result);
    }
}
