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

        public async Task<bool> ResetAsync(LoRaDevice loRaDevice)
        {
            loRaDevice.SetFcntUp(0);
            loRaDevice.SetFcntDown(0);
            return await InternalSaveChangesAsync(loRaDevice, force: true);
        }

        public ValueTask<int> NextFcntDown(LoRaDevice loRaDevice, int messageFcnt)
        {
            return new ValueTask<int>(loRaDevice.IncrementFcntDown(1));
        }

        public Task<bool> SaveChangesAsync(LoRaDevice loRaDevice) => InternalSaveChangesAsync(loRaDevice, force: false);

        private async Task<bool> InternalSaveChangesAsync(LoRaDevice loRaDevice, bool force)
        {
            if (loRaDevice.FCntUp % 10 == 0 || force)
            {
                return await loRaDevice.SaveFrameCountChangesAsync();
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
                //loRaDevice.AcceptFrameCountChanges();
            }
        }
    }
}