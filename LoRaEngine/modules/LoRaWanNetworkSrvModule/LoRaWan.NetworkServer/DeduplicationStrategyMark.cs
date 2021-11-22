// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class DeduplicationStrategyMark : ILoRaDeviceMessageDeduplicationStrategy
    {
        private readonly LoRaDevice loRaDevice;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public DeduplicationStrategyMark(LoRaDeviceAPIServiceBase loRaDeviceAPIService, LoRaDevice loRaDevice, ILogger<DeduplicationStrategyMark> logger)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDevice = loRaDevice;
            logger.LogDebug("deduplication Strategy: Mark");
        }

        public DeduplicationResult Process(DeduplicationResult result, uint fCntUp)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));

            result.CanProcess = true; // can always process. Message is marked if it is a duplicate
            return result;
        }

        public async Task<DeduplicationResult> ResolveDeduplication(uint fctUp, uint fcntDown, string gatewayId)
        {
            var result = await this.loRaDeviceAPIService.CheckDuplicateMsgAsync(this.loRaDevice.DevEUI, fctUp, gatewayId, fcntDown);
            return Process(result, fctUp);
        }
    }
}
