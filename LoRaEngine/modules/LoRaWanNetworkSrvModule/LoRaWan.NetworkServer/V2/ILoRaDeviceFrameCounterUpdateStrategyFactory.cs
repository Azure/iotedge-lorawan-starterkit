//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
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

        public void InitializeDeviceFrameCount(LoRaDevice loRaDevice)
        {

        }
    }

    public class SingleGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        public SingleGatewayFrameCounterUpdateStrategy()
        {
        }

        public async Task ResetAsync(LoRaDevice loraDevice)
        {
            loraDevice.SetFcntUp(0);
            loraDevice.SetFcntDown(0);
            await InternalUpdateAsync(loraDevice, force: true);
        }

        public ValueTask<int> NextFcntDown(LoRaDevice loraDevice)
        {
            return new ValueTask<int>(loraDevice.IncrementFcntDown(1));
        }



        public Task UpdateAsync(LoRaDevice loraDevice) => InternalUpdateAsync(loraDevice, force: false);

        private async Task InternalUpdateAsync(LoRaDevice loraDevice, bool force)
        {
            if (loraDevice.FCntUp % 10 == 0 || force)
            {
                await loraDevice.UpdateTwinAsync();
            }
        }

        private void UpdateFcntDown(LoRaDevice loraDevice)
        {
            loraDevice.IncrementFcntDown(1);
        }


        public void InitializeDeviceFrameCount(LoRaDevice loRaDevice)
        {
            // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
            // If device does not have gatewayid this will be handled by the service facade function (NextFCntDown)
            // here or at the deviceRegistry, what is better?
            if (loRaDevice.IsABP)
                loRaDevice.IncrementFcntDown(10);
        }
    }
}