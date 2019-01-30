// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    // Frame counter strategy for multi gateway scenarios
    // Frame Down counters is resolved by calling the LoRa device API. Only a single caller will received a valid frame counter (> 0)
    public class MultiGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        private readonly string gatewayID;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public MultiGatewayFrameCounterUpdateStrategy(string gatewayID, LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.gatewayID = gatewayID;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
        }

        public async Task<bool> ResetAsync(LoRaDevice loRaDevice)
        {
            loRaDevice.ResetFcnt();

            if (await this.InternalSaveChangesAsync(loRaDevice, force: true))
            {
                return await this.loRaDeviceAPIService.ABPFcntCacheResetAsync(loRaDevice.DevEUI);
            }

            return false;
        }

        public async ValueTask<int> NextFcntDown(LoRaDevice loRaDevice, int messageFcnt)
        {
            var result = await this.loRaDeviceAPIService.NextFCntDownAsync(
                devEUI: loRaDevice.DevEUI,
                fcntDown: loRaDevice.FCntDown,
                fcntUp: messageFcnt,
                gatewayId: this.gatewayID);

            if (result > 0)
            {
                loRaDevice.SetFcntDown(result);
            }

            return result;
        }

        public Task<bool> SaveChangesAsync(LoRaDevice loRaDevice) => this.InternalSaveChangesAsync(loRaDevice, force: false);

        private async Task<bool> InternalSaveChangesAsync(LoRaDevice loRaDevice, bool force)
        {
            if (loRaDevice.FCntUp % 10 == 0 || force)
            {
                return await loRaDevice.SaveFrameCountChangesAsync();
            }

            return true;
        }
    }
}