// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    public class FunctionBundlerPipelineExecuter : IPipelineExecutionContext
    {
        private static List<IFunctionBundlerExecutionItem> registeredHandlers = new List<IFunctionBundlerExecutionItem>
        {
            new DeduplicationExecutionItem(),
            new ADRExecutionItem(),
            new NextFCntDownExecutionItem()
        };

        public string DevEUI { get; private set; }

        public FunctionBundlerRequest Request { get; private set; }

        public FunctionBundlerResult Result { get; private set; } = new FunctionBundlerResult();

        public FunctionBundlerContext FunctionContext { get; private set; }

        public FunctionBundlerPipelineExecuter(
                                               string devEUI,
                                               FunctionBundlerRequest request,
                                               FunctionBundlerContext context)
        {
            this.DevEUI = devEUI;
            this.Request = request;
            this.FunctionContext = context;
        }

        public async Task<FunctionBundlerResult> Execute()
        {
            var executionPipeline = new List<IFunctionBundlerExecutionItem>(3);
            for (var i = 0; i < registeredHandlers.Count; i++)
            {
                var handler = registeredHandlers[i];
                if (handler.NeedsToExecute(this.Request.FunctionItems))
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
