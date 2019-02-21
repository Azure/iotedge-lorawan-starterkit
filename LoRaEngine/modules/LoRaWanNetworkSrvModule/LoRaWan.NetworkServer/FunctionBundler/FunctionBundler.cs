// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.ADR;

    public class FunctionBundler
    {
        private readonly LoRaDeviceAPIServiceBase deviceApi;
        private readonly FunctionBundlerRequest request;
        private readonly string devEui;
        private readonly ILoRaDeviceMessageDeduplicationStrategy deduplicationStrategy;

        internal FunctionBundler(string devEui, LoRaDeviceAPIServiceBase deviceApi, FunctionBundlerRequest request, ILoRaDeviceMessageDeduplicationStrategy deduplicationStrategy)
        {
            this.devEui = devEui;
            this.deviceApi = deviceApi;
            this.request = request;
            this.deduplicationStrategy = deduplicationStrategy;
        }

        public async Task<FunctionBundlerResult> Execute()
        {
            var result = await this.deviceApi.FunctionBundler(this.devEui, this.request);

            if (this.deduplicationStrategy != null && result.DeduplicationResult != null)
            {
                result.DeduplicationResult = this.deduplicationStrategy.Process(result.DeduplicationResult, this.request.ClientFCntUp);
            }

            return result;
        }
    }
}
