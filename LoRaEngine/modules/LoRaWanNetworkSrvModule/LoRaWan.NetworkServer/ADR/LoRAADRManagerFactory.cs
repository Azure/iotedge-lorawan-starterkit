// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using LoRaTools.ADR;

    public class LoRAADRManagerFactory : ILoRAADRManagerFactory
    {
        private LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public LoRAADRManagerFactory(LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
        }

        public ILoRaADRManager Create(ILoRaADRStrategyProvider strategyProvider, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy, LoRaDevice loRaDevice)
        {
            return !string.IsNullOrEmpty(loRaDevice.GatewayID)
                    ? new LoRaADRDefaultManager(new LoRaADRInMemoryStore(), strategyProvider, frameCounterStrategy, loRaDevice)
                    : new LoRaADRMultiGatewayManager(loRaDevice, this.loRaDeviceAPIService);
        }
    }
}
