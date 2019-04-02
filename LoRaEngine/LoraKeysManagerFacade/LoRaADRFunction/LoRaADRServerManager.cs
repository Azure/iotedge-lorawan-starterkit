// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.Azure.WebJobs;

    public class LoRaADRServerManager : LoRaADRManagerBase
    {
        private readonly ILoRaDeviceCacheStore deviceCacheStore;

        public LoRaADRServerManager(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider, ILoRaDeviceCacheStore deviceCacheStore)
            : base(store, strategyProvider)
        {
            this.deviceCacheStore = deviceCacheStore;
        }

        public override async Task<uint> NextFCntDown(string devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            var fcntCheck = new FCntCacheCheck(this.deviceCacheStore);
            return await fcntCheck.GetNextFCntDownAsync(devEUI, gatewayId, clientFCntUp, clientFCntDown);
        }
    }
}
