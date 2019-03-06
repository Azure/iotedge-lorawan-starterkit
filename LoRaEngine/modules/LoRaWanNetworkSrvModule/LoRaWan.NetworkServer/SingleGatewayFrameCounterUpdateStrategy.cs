// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class SingleGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy, ILoRaDeviceInitializer
    {
        public SingleGatewayFrameCounterUpdateStrategy()
        {
        }

        public async Task<bool> ResetAsync(LoRaDevice loRaDevice)
        {
            loRaDevice.ResetFcnt();
            return await this.InternalSaveChangesAsync(loRaDevice, force: true);
        }

        public ValueTask<uint> NextFcntDown(LoRaDevice loRaDevice, uint messageFcnt)
        {
            return new ValueTask<uint>(loRaDevice.IncrementFcntDown(1));
        }

        public Task<bool> SaveChangesAsync(LoRaDevice loRaDevice) => this.InternalSaveChangesAsync(loRaDevice, force: false);

        private SemaphoreSlim ss = new SemaphoreSlim(1);

        private async Task<bool> InternalSaveChangesAsync(LoRaDevice loRaDevice, bool force)
        {
            return await loRaDevice.SaveFrameCountChangesAsync(force);
        }

        // Initializes a device instance created
        // For ABP increment down count by 10 to take into consideration failed save attempts
        void ILoRaDeviceInitializer.Initialize(LoRaDevice loRaDevice)
        {
            // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
            if (loRaDevice.IsABP)
            {
                loRaDevice.IncrementFcntDownAndLastSaved(Constants.FCNT_DOWN_INCREMENTED_ON_ABP_DEVICE_LOAD);
            }
        }
    }
}