// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public class LoRaADRServerManager : LoRaADRManagerBase
    {
        private readonly ILoRaDeviceCacheStore deviceCacheStore;
        private readonly ILoggerFactory loggerFactory;

        public LoRaADRServerManager(ILoRaADRStore store,
                                    ILoRaADRStrategyProvider strategyProvider,
                                    ILoRaDeviceCacheStore deviceCacheStore,
                                    ILoggerFactory loggerFactory,
                                    ILogger<LoRaADRServerManager> logger)
            : base(store, strategyProvider, logger)
        {
            this.deviceCacheStore = deviceCacheStore;
            this.loggerFactory = loggerFactory;
        }

        public override async Task<uint> NextFCntDown(DevEui devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            var fcntCheck = new FCntCacheCheck(this.deviceCacheStore, this.loggerFactory.CreateLogger<FCntCacheCheck>());
            return await fcntCheck.GetNextFCntDownAsync(devEUI, gatewayId, clientFCntUp, clientFCntDown);
        }
    }
}
