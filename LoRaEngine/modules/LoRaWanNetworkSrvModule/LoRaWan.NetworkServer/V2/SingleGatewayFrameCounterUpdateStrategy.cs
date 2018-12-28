//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    public class SingleGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy, ILoRaDeviceInitializer
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
                await loraDevice.SaveFrameCountChangesAsync();
            }
        }

        private void UpdateFcntDown(LoRaDevice loraDevice)
        {
            loraDevice.IncrementFcntDown(1);
        }

        public void Initialize(LoRaDevice loRaDevice)
        {
            // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
            if (loRaDevice.IsABP)
                loRaDevice.IncrementFcntDown(10);
        }
    }
}