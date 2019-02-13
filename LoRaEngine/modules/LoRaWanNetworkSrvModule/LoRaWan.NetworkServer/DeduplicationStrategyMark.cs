// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class DeduplicationStrategyMark : ILoRaDeviceMessageDeduplicationStrategy
    {
        private readonly LoRaDevice loRaDevice;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public DeduplicationStrategyMark(LoRaDeviceAPIServiceBase loRaDeviceAPIService, LoRaDevice loRaDevice)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDevice = loRaDevice;
            Logger.Log("Deduplication Strategy: Mark", LogLevel.Debug);
        }

        public async Task<DeduplicationResult> ResolveDeduplication(int fcntUp, int? fcntDown, string gatewayId)
        {
            var result = await this.loRaDeviceAPIService.CheckDuplicateMsgAsync(this.loRaDevice.DevEUI, fcntUp, gatewayId, fcntDown);
            result.CanProcess = true; // can always process. Message is marked if it is a duplicate
            return result;
        }
    }
}
