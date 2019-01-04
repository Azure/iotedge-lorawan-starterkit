//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
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
            loRaDevice.SetFcntDown(0);
            loRaDevice.SetFcntUp(0);

            return await this.loRaDeviceAPIService.ABPFcntCacheResetAsync(loRaDevice.DevEUI);
        }

        public async ValueTask<int> NextFcntDown(LoRaDevice loRaDevice)
        {
            var result = await this.loRaDeviceAPIService.NextFCntDownAsync(
                devEUI: loRaDevice.DevEUI, 
                fcntDown: loRaDevice.FCntDown,
                fcntUp: loRaDevice.FCntUp,
                gatewayId: this.gatewayID);

            if (result > 0)
            {
                loRaDevice.SetFcntDown(result);
            }
            else
            {
                Logger.Log(loRaDevice.DevEUI, $"another gateway has already sent ack or downlink msg", Logger.LoggingLevel.Info);
            }

            return result;
        }

        public async Task<bool> SaveChangesAsync(LoRaDevice loRaDevice)
        {            
            if (loRaDevice.FCntUp % 10 == 0)
            {
                return await loRaDevice.SaveFrameCountChangesAsync();
            }

            return true;
        }
    }
}