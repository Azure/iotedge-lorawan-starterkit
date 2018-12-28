//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace LoRaWan.NetworkServer.V2
{
    public class FrameCounterLoRaDeviceInitializer : ILoRaDeviceInitializer
    {
        private readonly ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory;

        public FrameCounterLoRaDeviceInitializer(string gatewayID, ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory)
        {
            GatewayID = gatewayID;
            this.frameCounterUpdateStrategyFactory = frameCounterUpdateStrategyFactory;
        }

        public string GatewayID { get; }

        public void Initialize(LoRaDevice loRaDevice)
        {
            var isMultiGateway = !string.Equals(this.GatewayID, loRaDevice.GatewayID);
            var strategy = isMultiGateway ? frameCounterUpdateStrategyFactory.GetMultiGatewayStrategy() : frameCounterUpdateStrategyFactory.GetSingleGatewayStrategy();
            if (strategy is ILoRaDeviceInitializer initializer)
            {
                initializer.Initialize(loRaDevice);
            }
        }
    }
}