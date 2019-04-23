// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;

    class TestMultiGatewayUpdateFrameStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        SemaphoreSlim nextFcntDownLock = new SemaphoreSlim(1);

        public async ValueTask<uint> NextFcntDown(LoRaDevice loRaDevice, uint messageFcnt)
        {
            await this.nextFcntDownLock.WaitAsync();
            try
            {
                return loRaDevice.IncrementFcntDown(1);
            }
            finally
            {
                this.nextFcntDownLock.Release();
            }
        }

        public Task<bool> ResetAsync(LoRaDevice loRaDevice, uint fcntUp, string gatewayId)
        {
            loRaDevice.ResetFcnt();

            return Task.FromResult(true);
        }

        public Task<bool> SaveChangesAsync(LoRaDevice loRaDevice) => loRaDevice.SaveChangesAsync();
    }
}
