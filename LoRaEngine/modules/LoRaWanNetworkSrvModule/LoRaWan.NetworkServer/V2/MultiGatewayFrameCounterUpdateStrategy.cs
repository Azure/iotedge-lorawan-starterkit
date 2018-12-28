//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    public class MultiGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        private readonly string gatewayID;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public MultiGatewayFrameCounterUpdateStrategy(string gatewayID, LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.gatewayID = gatewayID;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
        }

        public async Task ResetAsync(LoRaDevice loraDeviceInfo)
        {
            await this.loRaDeviceAPIService.ABPFcntCacheResetAsync(loraDeviceInfo.DevEUI);
            loraDeviceInfo.SetFcntDown(0);
            loraDeviceInfo.SetFcntUp(0);
        }

        public async ValueTask<int> NextFcntDown(LoRaDevice loraDeviceInfo)
        {
            var result = await this.loRaDeviceAPIService.NextFCntDownAsync(
                devEUI: loraDeviceInfo.DevEUI, 
                fcntDown: loraDeviceInfo.FCntDown,
                fcntUp: loraDeviceInfo.FCntUp,
                gatewayId: this.gatewayID);

            loraDeviceInfo.SetFcntDown(result);

            return result;
        }

        public Task UpdateAsync(LoRaDevice loraDeviceInfo)
        {
            throw new NotImplementedException();
        }
    }
}