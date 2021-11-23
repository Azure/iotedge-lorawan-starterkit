// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class FunctionBundler
    {
        private readonly LoRaDeviceAPIServiceBase deviceApi;
        private readonly FunctionBundlerRequest request;
        private readonly string devEui;
        private readonly IList<IFunctionBundlerExecutionItem> executionItems;
        private readonly FunctionBundlerExecutionContext executionContext;
        private readonly ILogger<FunctionBundler> logger;

        internal FunctionBundler(string devEui,
                                 LoRaDeviceAPIServiceBase deviceApi,
                                 FunctionBundlerRequest request,
                                 IList<IFunctionBundlerExecutionItem> executionItems,
                                 FunctionBundlerExecutionContext executionContext,
                                 ILogger<FunctionBundler> logger)
        {
            this.devEui = devEui;
            this.deviceApi = deviceApi;
            this.request = request;
            this.executionItems = executionItems;
            this.executionContext = executionContext;
            this.logger = logger;
        }

        public async Task<FunctionBundlerResult> Execute()
        {
            var result = await this.deviceApi.ExecuteFunctionBundlerAsync(this.devEui, this.request);

            foreach (var item in this.executionItems)
                item.ProcessResult(this.executionContext, result);

            if (this.logger.IsEnabled(LogLevel.Debug))
                this.logger.LogDebug($"FunctionBundler result: {JsonSerializer.Serialize(result)}");

            return result;
        }
    }
}
