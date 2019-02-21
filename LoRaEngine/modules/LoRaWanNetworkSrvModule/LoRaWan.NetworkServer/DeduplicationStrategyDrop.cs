// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class DeduplicationStrategyDrop : ILoRaDeviceMessageDeduplicationStrategy
    {
        private readonly LoRaDevice loRaDevice;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public DeduplicationStrategyDrop(LoRaDeviceAPIServiceBase loRaDeviceAPIService, LoRaDevice loRaDevice)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDevice = loRaDevice;
            Logger.Log(this.loRaDevice.DevEUI, "Deduplication Strategy: Drop", LogLevel.Debug);
        }

        public DeduplicationResult Process(DeduplicationResult result, int fCntUp)
        {
            result.CanProcess = !result.IsDuplicate;

            if (result.IsDuplicate)
            {
                Logger.Log(this.loRaDevice.DevEUI, $"Duplicate message '{fCntUp}' is dropped.", LogLevel.Debug);
            }

            return result;
        }

        public async Task<DeduplicationResult> ResolveDeduplication(int fcntUp, int fcntDown, string gatewayId)
        {
            var result = await this.loRaDeviceAPIService.CheckDuplicateMsgAsync(this.loRaDevice.DevEUI, fcntUp, gatewayId, fcntDown);

            return this.Process(result, fcntUp);
        }
    }
}
