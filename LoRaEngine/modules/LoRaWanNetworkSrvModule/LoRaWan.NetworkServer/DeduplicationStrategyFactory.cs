// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Microsoft.Extensions.Logging;

    public class DeduplicationStrategyFactory : IDeduplicationStrategyFactory
    {
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public DeduplicationStrategyFactory(LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
        }

        public ILoRaDeviceMessageDeduplicationStrategy Create(LoRaDevice loRaDevice)
        {
            if (!string.IsNullOrEmpty(loRaDevice.GatewayID))
            {
                Logger.Log(loRaDevice.DevEUI, "LoRa device has a specific gateway assigned. Ignoring deduplication as it is not applicable.", LogLevel.Debug);
                return null;
            }

            switch (loRaDevice.Deduplication)
            {
                case DeduplicationMode.Drop: return new DeduplicationStrategyDrop(this.loRaDeviceAPIService, loRaDevice);
                case DeduplicationMode.Mark: return new DeduplicationStrategyMark(this.loRaDeviceAPIService, loRaDevice);
                default:
                    {
                        Logger.Log(loRaDevice.DevEUI, "no Deduplication Strategy selected", LogLevel.Debug);
                        return null;
                    }
            }
        }
    }
}
