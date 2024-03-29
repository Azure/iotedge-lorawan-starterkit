// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;

    public class FrameCounterLoRaDeviceInitializer : ILoRaDeviceInitializer
    {
        private readonly string gatewayID;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;

        public FrameCounterLoRaDeviceInitializer(string gatewayID, ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider)
        {
            this.gatewayID = gatewayID;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
        }

        public void Initialize(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            if (loRaDevice.IsOurDevice)
            {
                var strategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
                if (strategy is not null and ILoRaDeviceInitializer initializer)
                {
                    initializer.Initialize(loRaDevice);
                }
            }
        }
    }
}
