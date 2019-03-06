// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    public interface IFunctionBundlerExecutionItem
    {
        bool NeedsToExecute(FunctionBundlerItemType item);

        int Priority { get; }

        Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context);

        Task OnAbortExecutionAsync(IPipelineExecutionContext context);
    }
}
