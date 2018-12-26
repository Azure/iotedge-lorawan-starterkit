//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public interface ILoRaDeviceFrameCounterUpdateStrategyFactory
    {
        ILoRaDeviceFrameCounterUpdateStrategy GetMultiGatewayStrategy();

        ILoRaDeviceFrameCounterUpdateStrategy GetSingleGatewayStrategy();

    }



    public class DefaultLoRaDeviceFrameCounterUpdateStrategyFactory : ILoRaDeviceFrameCounterUpdateStrategyFactory
    {
        private readonly MultiGatewayFrameCounterUpdateStrategy multiGateway;
        private readonly SingleGatewayFrameCounterUpdateStrategy singleGateway;

        public DefaultLoRaDeviceFrameCounterUpdateStrategyFactory(string gatewayID, LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.multiGateway = new MultiGatewayFrameCounterUpdateStrategy(gatewayID, loRaDeviceAPIService);
            this.singleGateway = new SingleGatewayFrameCounterUpdateStrategy();
        }

        public ILoRaDeviceFrameCounterUpdateStrategy GetMultiGatewayStrategy() => this.multiGateway;

        public ILoRaDeviceFrameCounterUpdateStrategy GetSingleGatewayStrategy() => this.singleGateway;
    }

    public class MultiGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        private readonly string gatewayID;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;

        public MultiGatewayFrameCounterUpdateStrategy(string gatewayID, LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.gatewayID = gatewayID;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
        }

        public async Task ResetAsync(ILoRaDevice loraDeviceInfo)
        {
            await this.loRaDeviceAPIService.ABPFcntCacheResetAsync(loraDeviceInfo.DevEUI);
            loraDeviceInfo.SetFcntDown(0);
            loraDeviceInfo.SetFcntUp(0);
        }

        public async ValueTask<int> NextFcntDown(ILoRaDevice loraDeviceInfo)
        {
            var result = await this.loRaDeviceAPIService.NextFCntDownAsync(
                devEUI: loraDeviceInfo.DevEUI, 
                fcntDown: loraDeviceInfo.FcntDown,
                fcntUp: loraDeviceInfo.FcntUp,
                gatewayId: this.gatewayID);

            loraDeviceInfo.SetFcntDown(result);

            return result;
        }

        public Task UpdateAsync(ILoRaDevice loraDeviceInfo)
        {
            throw new NotImplementedException();
        }
    }

    public class SingleGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        public SingleGatewayFrameCounterUpdateStrategy()
        {
        }

        public async Task ResetAsync(ILoRaDevice loraDeviceInfo)
        {
            loraDeviceInfo.SetFcntUp(0);
            loraDeviceInfo.SetFcntDown(0);
            await InternalUpdateAsync(loraDeviceInfo, force: true);
        }

        public ValueTask<int> NextFcntDown(ILoRaDevice loraDeviceInfo)
        {
            return new ValueTask<int>(loraDeviceInfo.IncrementFcntDown(1));
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
            loraDeviceInfo.IncrementFcntDown(1);
        }
    }
}