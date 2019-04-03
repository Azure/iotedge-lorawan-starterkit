// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Threading.Tasks;

    static class LockDevAddrHelper
    {
        private const string FullUpdateKey = "fullUpdateKey";
        private const string GlobalDevAddrUpdateKey = "globalUpdateKey";
        private const string DeltaUpdateKey = "deltaUpdateKey";

        public static async Task TakeLocksAsync(ILoRaDeviceCacheStore loRaDeviceCache, string[] lockNames)
        {
            if (lockNames?.Length > 0)
            {
                foreach (var locks in lockNames)
                {
                    await loRaDeviceCache.LockTakeAsync(locks, locks, TimeSpan.FromMinutes(3));
                }
            }
        }

        public static void ReleaseAllLocks(ILoRaDeviceCacheStore loRaDeviceCache)
        {
            loRaDeviceCache.KeyDelete(GlobalDevAddrUpdateKey);
            loRaDeviceCache.KeyDelete(FullUpdateKey);
            loRaDeviceCache.KeyDelete(DeltaUpdateKey);
        }

        public static async Task PrepareLocksForTests(ILoRaDeviceCacheStore loRaDeviceCache, string[] neededLocksForTestToRun, string[] locksGuideTest)
        {
            LockDevAddrHelper.ReleaseAllLocks(loRaDeviceCache);
            await LockDevAddrHelper.TakeLocksAsync(loRaDeviceCache, locksGuideTest);
        }
    }
}
