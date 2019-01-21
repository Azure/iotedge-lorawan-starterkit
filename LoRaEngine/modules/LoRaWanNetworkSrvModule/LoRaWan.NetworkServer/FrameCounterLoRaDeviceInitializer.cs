//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace LoRaWan.NetworkServer
{
    public class FrameCounterLoRaDeviceInitializer : ILoRaDeviceInitializer
    {
        private readonly string gatewayID;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory;

        public FrameCounterLoRaDeviceInitializer(string gatewayID, ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory)
        {
            this.gatewayID = gatewayID;
            this.frameCounterUpdateStrategyFactory = frameCounterUpdateStrategyFactory;
        }


        public void Initialize(LoRaDevice loRaDevice)
        {
            var isMultiGateway = !string.Equals(this.gatewayID, loRaDevice.GatewayID, StringComparison.InvariantCultureIgnoreCase);
            var strategy = isMultiGateway ? frameCounterUpdateStrategyFactory.GetMultiGatewayStrategy() : frameCounterUpdateStrategyFactory.GetSingleGatewayStrategy();
            if (strategy is ILoRaDeviceInitializer initializer)
            {
                initializer.Initialize(loRaDevice);
            }
        }
    }
}