// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class DeduplicationStrategyDrop : ILoRaDeviceMessageDeduplicationStrategy
    {
        private readonly LoRaDevice loRaDevice;
        private readonly ILogger<DeduplicationStrategyDrop> logger;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public DeduplicationStrategyDrop(LoRaDeviceAPIServiceBase loRaDeviceAPIService, LoRaDevice loRaDevice, ILogger<DeduplicationStrategyDrop> logger)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDevice = loRaDevice;
            this.logger = logger;
            StaticLogger.Log(this.loRaDevice.DevEUI, "deduplication Strategy: Drop", LogLevel.Debug);
        }

        public DeduplicationResult Process(DeduplicationResult result, uint fCntUp)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));

            result.CanProcess = !result.IsDuplicate;

            if (result.IsDuplicate)
            {
                this.logger.LogDebug($"duplicate message '{fCntUp}' is dropped.");
            }

            return result;
        }

        public async Task<DeduplicationResult> ResolveDeduplication(uint fctUp, uint fcntDown, string gatewayId)
        {
            var result = await this.loRaDeviceAPIService.CheckDuplicateMsgAsync(this.loRaDevice.DevEUI, fctUp, gatewayId, fcntDown);

            return Process(result, fctUp);
        }
    }
}
