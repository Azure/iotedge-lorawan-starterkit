// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    public interface IFunctionBundlerExecutionItem
    {
        bool NeedsToExecute(FunctionBundlerItem item);

        Task<FunctionBundlerExecutionState> Execute(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory);

        Task OnAbortExecution(string devEUI, FunctionBundlerRequest request, FunctionBundlerResult result, string functionAppDirectory);
    }
}
