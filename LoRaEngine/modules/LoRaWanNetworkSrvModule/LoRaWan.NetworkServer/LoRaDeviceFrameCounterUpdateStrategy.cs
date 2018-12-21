//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public interface ILoRaDeviceFrameCounterUpdateStrategy
    {
        Task ForceUpdateAsync(ILoRaDevice loraDeviceInfo, UInt32 fcntUp, UInt32 fcntDown);
        Task<UInt32> NextFcntDown(ILoRaDevice loraDeviceInfo);
        Task UpdateAsync(ILoRaDevice loraDeviceInfo);
    }

    public class LoRaDeviceFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        private readonly string gatewayID;

        public LoRaDeviceFrameCounterUpdateStrategy(string gatewayID)
        {
            this.gatewayID = gatewayID;
        }

        public async Task ForceUpdateAsync(ILoRaDevice loraDeviceInfo, UInt32 fcntUp, UInt32 fcntDown)
        {
            loraDeviceInfo.SetFcntUp(fcntUp);
            loraDeviceInfo.SetFcntDown(fcntDown);
            await InternalUpdateAsync(loraDeviceInfo, force: true);
        }

        public async Task<UInt32> NextFcntDown(ILoRaDevice loraDeviceInfo)
        {
            UInt32 result = 0;
            // make it thread safe
            if (this.gatewayID == loraDeviceInfo.GatewayID)
            {
                result = loraDeviceInfo.IncrementFcntDown(1);
            }
            else
            {
                result = await AzureFunctionNextFcntDownAsync(loraDeviceInfo);
                loraDeviceInfo.SetFcntDown(result);
            }

            return result;
        }

        private Task<UInt32> AzureFunctionNextFcntDownAsync(ILoRaDevice loraDeviceInfo)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(ILoRaDevice loraDeviceInfo) => InternalUpdateAsync(loraDeviceInfo, force: false);

        private async Task InternalUpdateAsync(ILoRaDevice loraDeviceInfo, bool force)
        {
            if (loraDeviceInfo.FcntUp % 10 == 0 || force)
            {
                await loraDeviceInfo.UpdateTwinAsync(loraDeviceInfo.GetTwinProperties());
            }
        }

        private void UpdateFcntDown(ILoRaDevice loraDeviceInfo)
        {
            if (loraDeviceInfo.GatewayID == this.gatewayID)
                loraDeviceInfo.IncrementFcntDown(1);

        }
    }
}