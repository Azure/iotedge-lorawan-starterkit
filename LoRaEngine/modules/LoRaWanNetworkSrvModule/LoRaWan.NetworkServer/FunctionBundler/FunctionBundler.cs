// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class FunctionBundler
    {
        private readonly LoRaDeviceAPIServiceBase deviceApi;
        private readonly FunctionBundlerRequest request;
        private readonly string devEui;
        private readonly IList<IFunctionBundlerExecutionItem> executionItems;
        private readonly FunctionBundlerExecutionContext executionContext;

        internal FunctionBundler(string devEui, LoRaDeviceAPIServiceBase deviceApi, FunctionBundlerRequest request, IList<IFunctionBundlerExecutionItem> executionItems, FunctionBundlerExecutionContext executionContext)
        {
            this.devEui = devEui;
            this.deviceApi = deviceApi;
            this.request = request;
            this.executionItems = executionItems;
            this.executionContext = executionContext;
        }

        public async Task<FunctionBundlerResult> Execute()
        {
            var result = await this.deviceApi.ExecuteFunctionBundlerAsync(this.devEui, this.request);

            for (var i = 0; i < this.executionItems.Count; i++)
            {
                this.executionItems[i].ProcessResult(this.executionContext, result);
            }

            Logger.Log(this.devEui, "FunctionBundler result: ", result, LogLevel.Debug);

            return result;
        }
    }
}
