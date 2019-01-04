using LoRaWan.NetworkServer.V2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.Test
{
    class TestMultiGatewayUpdateFrameStrategy : ILoRaDeviceFrameCounterUpdateStrategy
    {
        SemaphoreSlim nextFcntDownLock = new SemaphoreSlim(1);

        public async ValueTask<int> NextFcntDown(LoRaDevice loRaDevice)
        {
            await nextFcntDownLock.WaitAsync();
            try
            {
                return loRaDevice.IncrementFcntDown(1);
            }
            finally
            {
                nextFcntDownLock.Release();
            }
                
        }

        public Task<bool> ResetAsync(LoRaDevice loRaDevice)
        {
            loRaDevice.SetFcntDown(0);
            loRaDevice.SetFcntUp(0);

            return Task.FromResult(true);
        }

        public async Task<bool> SaveChangesAsync(LoRaDevice loRaDevice)
        {
            if (loRaDevice.FCntUp % 10 == 0)
            {
                return await loRaDevice.SaveFrameCountChangesAsync();
            }

            return true;
        }
    }
}
