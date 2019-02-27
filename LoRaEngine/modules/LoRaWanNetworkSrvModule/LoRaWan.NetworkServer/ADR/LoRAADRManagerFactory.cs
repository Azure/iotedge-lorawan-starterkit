// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using LoRaTools.ADR;

    public class LoRAADRManagerFactory : ILoRAADRManagerFactory
    {
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private static readonly object InMemoryStoreLock = new object();
        private static volatile LoRaADRInMemoryStore inMemoryStore;

        public LoRAADRManagerFactory(LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
        }

        public ILoRaADRManager Create(ILoRaADRStrategyProvider strategyProvider, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy, LoRaDevice loRaDevice)
        {
            return !string.IsNullOrEmpty(loRaDevice.GatewayID)
                    ? new LoRaADRDefaultManager(CurrentInMemoryStore, strategyProvider, frameCounterStrategy, loRaDevice)
                    : new LoRaADRMultiGatewayManager(loRaDevice, this.loRaDeviceAPIService);
        }

        private static LoRaADRInMemoryStore CurrentInMemoryStore
        {
            get
            {
                if (inMemoryStore != null)
                {
                    return inMemoryStore;
                }

                lock (InMemoryStoreLock)
                {
                    if (inMemoryStore == null)
                    {
                        inMemoryStore = new LoRaADRInMemoryStore();
                    }
                }

                return inMemoryStore;
            }
        }
    }
}
