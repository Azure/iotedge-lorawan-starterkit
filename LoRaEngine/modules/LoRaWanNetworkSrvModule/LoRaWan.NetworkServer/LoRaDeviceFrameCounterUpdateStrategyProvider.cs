// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public class LoRaDeviceFrameCounterUpdateStrategyProvider : ILoRaDeviceFrameCounterUpdateStrategyProvider
    {
        private readonly MultiGatewayFrameCounterUpdateStrategy multiGateway;
        private readonly SingleGatewayFrameCounterUpdateStrategy singleGateway;

        public LoRaDeviceFrameCounterUpdateStrategyProvider(string gatewayID, LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.multiGateway = new MultiGatewayFrameCounterUpdateStrategy(gatewayID, loRaDeviceAPIService);
            this.singleGateway = new SingleGatewayFrameCounterUpdateStrategy();
        }

        public ILoRaDeviceFrameCounterUpdateStrategy GetMultiGatewayStrategy() => this.multiGateway;

        public ILoRaDeviceFrameCounterUpdateStrategy GetSingleGatewayStrategy() => this.singleGateway;
    }
}