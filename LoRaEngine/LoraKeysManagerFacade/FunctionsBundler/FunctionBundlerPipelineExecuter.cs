// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class FunctionBundlerPipelineExecuter : IPipelineExecutionContext
    {
        private readonly IFunctionBundlerExecutionItem[] registeredHandlers;

        public string DevEUI { get; private set; }

        public FunctionBundlerRequest Request { get; private set; }

        public FunctionBundlerResult Result { get; private set; } = new FunctionBundlerResult();

        public ILogger Logger { get; private set; }

        public FunctionBundlerPipelineExecuter(
                                               IFunctionBundlerExecutionItem[] registeredHandlers,
                                               string devEUI,
                                               FunctionBundlerRequest request,
                                               ILogger logger = null)
        {
            this.registeredHandlers = registeredHandlers;
            this.DevEUI = devEUI;
            this.Request = request;
            this.Logger = logger ?? NullLogger.Instance;
        }

        public async Task<FunctionBundlerResult> Execute()
        {
            var executionPipeline = new List<IFunctionBundlerExecutionItem>(this.registeredHandlers.Length);
            for (var i = 0; i < this.registeredHandlers.Length; i++)
            {
                var handler = this.registeredHandlers[i];
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
                        state = await handler.ExecuteAsync(this);
                        break;
                    case FunctionBundlerExecutionState.Abort:
                        await handler.OnAbortExecutionAsync(this);
                        break;
                }
            }

            return this.Result;
        }
    }
}
