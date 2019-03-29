// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;

    public class LoRaDeviceFrameCounterUpdateStrategyProvider : ILoRaDeviceFrameCounterUpdateStrategyProvider
    {
        private readonly string gatewayID;
        private readonly MultiGatewayFrameCounterUpdateStrategy multiGateway;
        private readonly SingleGatewayFrameCounterUpdateStrategy singleGateway;

        public LoRaDeviceFrameCounterUpdateStrategyProvider(string gatewayID, LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.gatewayID = gatewayID;
            this.multiGateway = new MultiGatewayFrameCounterUpdateStrategy(gatewayID, loRaDeviceAPIService);
            this.singleGateway = new SingleGatewayFrameCounterUpdateStrategy();
        }

        public ILoRaDeviceFrameCounterUpdateStrategy GetStrategy(string deviceGatewayID)
        {
            if (string.IsNullOrEmpty(deviceGatewayID))
                return this.multiGateway;

            if (string.Equals(this.gatewayID, deviceGatewayID, StringComparison.InvariantCultureIgnoreCase))
                return this.singleGateway;

            return null;
        }
    }
}