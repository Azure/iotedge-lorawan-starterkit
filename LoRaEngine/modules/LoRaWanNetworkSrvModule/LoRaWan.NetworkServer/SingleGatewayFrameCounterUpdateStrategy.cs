// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    public sealed class SingleGatewayFrameCounterUpdateStrategy : ILoRaDeviceFrameCounterUpdateStrategy, ILoRaDeviceInitializer
    {
        public Task<bool> ResetAsync(LoRaDevice loRaDevice, uint fcntUp, string gatewayId)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            loRaDevice.ResetFcnt();
            return Task.FromResult(true); // always able to reset locally
        }

        public ValueTask<uint> NextFcntDown(LoRaDevice loRaDevice, uint messageFcnt)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));
            return new ValueTask<uint>(loRaDevice.IncrementFcntDown(1));
        }

        public Task<bool> SaveChangesAsync(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));
            return InternalSaveChangesAsync(loRaDevice, force: false);
        }

        private static async Task<bool> InternalSaveChangesAsync(LoRaDevice loRaDevice, bool force)
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
                _ = loRaDevice.IncrementFcntDown(Constants.MaxFcntUnsavedDelta - 1);
            }
        }
    }
}
