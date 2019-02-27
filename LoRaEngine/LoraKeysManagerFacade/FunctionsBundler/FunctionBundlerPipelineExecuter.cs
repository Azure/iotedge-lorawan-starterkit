// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class FunctionBundlerPipelineExecuter : IPipelineExecutionContext
    {
        private readonly string devEUI;
        private readonly FunctionBundlerRequest request;
        private readonly string functionAppDirectory;

        private FunctionBundlerResult result = new FunctionBundlerResult();

        private static List<IFunctionBundlerExecutionItem> registeredHandlers = new List<IFunctionBundlerExecutionItem>
        {
            new DeduplicationExecutionItem(),
            new ADRExecutionItem(),
            new NextFCntDownExecutionItem()
        };

        public string DevEUI => this.devEUI;

        public FunctionBundlerRequest Request => this.request;

        public FunctionBundlerResult Result => this.result;

        public string FunctionAppDirectory => this.functionAppDirectory;

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

            var state = FunctionBundlerExecutionState.Continue;

            for (var i = 0; i < executionPipeline.Count; i++)
            {
                var handler = executionPipeline[i];
                switch (state)
                {
                    case FunctionBundlerExecutionState.Continue:
                        state = await handler.Execute(this);
                        break;
                    case FunctionBundlerExecutionState.Abort:
                        await handler.OnAbortExecution(this);
                        break;
                }
            }

            return this.Result;
        }
    }
}
