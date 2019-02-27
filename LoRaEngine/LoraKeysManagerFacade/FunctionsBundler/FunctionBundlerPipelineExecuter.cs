// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class FunctionBundlerPipelineExecuter
    {
        private readonly string devEUI;
        private readonly FunctionBundlerRequest request;
        private readonly string functionAppDirectory;

        private static List<IFunctionBundlerExecutionItem> registeredHandlers = new List<IFunctionBundlerExecutionItem>
        {
            new DeduplicationExecutionItem(),
            new ADRExecutionItem(),
            new NextFCntDownExecutionItem()
        };

        public FunctionBundlerPipelineExecuter(string devEUI, FunctionBundlerRequest request, string functionAppDirectory)
        {
            this.devEUI = devEUI;
            this.request = request;
            this.functionAppDirectory = functionAppDirectory;
        }

        public async Task<FunctionBundlerResult> Execute()
        {
            var executionPipeline = new List<IFunctionBundlerExecutionItem>(3);
            for (var i = 0; i < registeredHandlers.Count; i++)
            {
                var handler = registeredHandlers[i];
                if (handler.NeedsToExecute(this.request.FunctionItems))
                {
                    executionPipeline.Add(handler);
                }
            }

            var result = new FunctionBundlerResult();

            var state = FunctionBundlerExecutionState.Continue;

            for (var i = 0; i < executionPipeline.Count; i++)
            {
                var handler = executionPipeline[i];
                switch (state)
                {
                    case FunctionBundlerExecutionState.Continue:
                        state = await handler.Execute(this.devEUI, this.request, result, this.functionAppDirectory);
                        break;
                    case FunctionBundlerExecutionState.Abort:
                        await handler.OnAbortExecution(this.devEUI, this.request, result, this.functionAppDirectory);
                        break;
                }
            }

            return result;
        }
    }
}
