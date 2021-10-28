// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using LoRaTools.ADR;
    using System;

    public class LoRAADRManagerFactory : ILoRAADRManagerFactory
    {
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private static readonly object InMemoryStoreLock = new object();
        private static volatile LoRaADRInMemoryStore inMemoryStore;

        public LoRAADRManagerFactory(LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
        }

        public ILoRaADRManager Create(ILoRaADRStrategyProvider strategyProvider,
                                      ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy,
                                      LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

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
#pragma warning disable CA1508 // Avoid dead conditional code
                    // False positive.
                    if (inMemoryStore == null)
#pragma warning restore CA1508 // Avoid dead conditional code
                    {
                        inMemoryStore = new LoRaADRInMemoryStore();
                    }
                }

                return inMemoryStore;
            }
        }
    }
}
