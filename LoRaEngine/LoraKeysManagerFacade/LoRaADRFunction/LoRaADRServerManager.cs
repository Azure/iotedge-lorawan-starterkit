// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.Azure.WebJobs;

    public class LoRaADRServerManager : LoRaADRManagerBase
    {
        private readonly string functionAppDirectory;

        public LoRaADRServerManager(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider, string functionsAppDirectory)
            : base(store, strategyProvider)
        {
            this.functionAppDirectory = functionsAppDirectory;
        }

        public override Task<int> NextFCntDown(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            return Task.FromResult(FCntCacheCheck.GetNextFCntDown(devEUI, gatewayId, clientFCntUp, clientFCntDown, this.functionAppDirectory));
        }
    }
}
