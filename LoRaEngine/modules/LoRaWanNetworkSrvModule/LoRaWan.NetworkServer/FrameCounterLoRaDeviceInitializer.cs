// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;

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
            if (loRaDevice.IsOurDevice)
            {
                var isMultiGateway = !string.Equals(this.gatewayID, loRaDevice.GatewayID, StringComparison.InvariantCultureIgnoreCase);
                var strategy = isMultiGateway ? this.frameCounterUpdateStrategyFactory.GetMultiGatewayStrategy() : this.frameCounterUpdateStrategyFactory.GetSingleGatewayStrategy();
                if (strategy is ILoRaDeviceInitializer initializer)
                {
                    initializer.Initialize(loRaDevice);
                }
            }
        }
    }
}