// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
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

        public async Task<bool> ResetAsync(LoRaDevice loRaDevice, uint fcntUp, string gatewayId)
        {
            if (loRaDevice is null) throw new System.ArgumentNullException(nameof(loRaDevice));

            loRaDevice.ResetFcnt();

            return await this.loRaDeviceAPIService.ABPFcntCacheResetAsync(loRaDevice.DevEUI, fcntUp, gatewayId);
        }

        public async ValueTask<uint> NextFcntDown(LoRaDevice loRaDevice, uint messageFcnt)
        {
            if (loRaDevice is null) throw new System.ArgumentNullException(nameof(loRaDevice));

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

        public Task<bool> SaveChangesAsync(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new System.ArgumentNullException(nameof(loRaDevice));
            return InternalSaveChangesAsync(loRaDevice, force: false);
        }

        private static async Task<bool> InternalSaveChangesAsync(LoRaDevice loRaDevice, bool force)
        {
            return await loRaDevice.SaveChangesAsync(force: force);
        }
    }
}
