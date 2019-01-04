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

        public async Task<bool> ResetAsync(LoRaDevice loraDevice)
        {
            loraDevice.SetFcntUp(0);
            loraDevice.SetFcntDown(0);
            return await InternalSaveChangesAsync(loraDevice, force: true);
        }

        public ValueTask<int> NextFcntDown(LoRaDevice loraDevice)
        {
            return new ValueTask<int>(loraDevice.IncrementFcntDown(1));
        }

        public Task<bool> SaveChangesAsync(LoRaDevice loraDevice) => InternalSaveChangesAsync(loraDevice, force: false);

        private async Task<bool> InternalSaveChangesAsync(LoRaDevice loraDevice, bool force)
        {
            if (loraDevice.FCntUp % 10 == 0 || force)
            {
                return await loraDevice.SaveFrameCountChangesAsync();
            }

            return true;
        }

        // Initializes a device instance created
        // For ABP increment down count by 10 to take into consideration failed save attempts
        void ILoRaDeviceInitializer.Initialize(LoRaDevice loRaDevice)
        {
            // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
            if (loRaDevice.IsABP)
            {
                loRaDevice.IncrementFcntDown(10);

                // do not save the changes
                loRaDevice.AcceptFrameCountChanges();
            }
        }
    }
}