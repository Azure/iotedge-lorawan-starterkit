// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;

    public class SingleGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy, ILoRaDeviceInitializer
    {
        public SingleGatewayFrameCounterUpdateStrategy()
        {
        }

        public async Task<bool> ResetAsync(LoRaDevice loRaDevice, uint fcntUp, string gatewayId)
        {
            loRaDevice.ResetFcnt();
            return await this.InternalSaveChangesAsync(loRaDevice, force: true);
        }

        public ValueTask<uint> NextFcntDown(LoRaDevice loRaDevice, uint messageFcnt)
        {
            return new ValueTask<uint>(loRaDevice.IncrementFcntDown(1));
        }

        public Task<bool> SaveChangesAsync(LoRaDevice loRaDevice) => this.InternalSaveChangesAsync(loRaDevice, force: false);

        private async Task<bool> InternalSaveChangesAsync(LoRaDevice loRaDevice, bool force)
        {
            return await loRaDevice.SaveChangesAsync(force: force);
        }

        // Initializes a device instance created
        // For ABP increment down count by 10 to take into consideration failed save attempts
        void ILoRaDeviceInitializer.Initialize(LoRaDevice loRaDevice)
        {
            // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
            if (loRaDevice.IsABP)
            {
                // Increment so that the next frame count down causes the count to be saved
                loRaDevice.IncrementFcntDown(Constants.MAX_FCNT_UNSAVED_DELTA - 1);
            }
        }
    }
}