// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    internal class LoRaInMemoryDeviceStore : ILoRaDeviceCacheStore
    {
        private readonly Dictionary<string, string> cache;
        private readonly Dictionary<string, SemaphoreSlim> locks;

        public LoRaInMemoryDeviceStore()
        {
            this.cache = new Dictionary<string, string>();
            this.locks = new Dictionary<string, SemaphoreSlim>();
        }

        public bool KeyDelete(string key)
        {
            return this.cache.Remove(key);
        }

        public bool LockRelease(string key, string value)
        {
            lock (this.locks)
            {
                if (this.locks.TryGetValue(key + value, out var sem))
                {
                    sem.Release();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public bool LockTake(string key, string value, TimeSpan timeout)
        {
            SemaphoreSlim waiter = null;
            var lockKey = key + value;

            lock (this.locks)
            {
                if (!this.locks.TryGetValue(lockKey, out waiter))
                {
                    this.locks[lockKey] = new SemaphoreSlim(0);
                    return true;
                }
            }

            if (waiter.Wait((int)timeout.TotalMilliseconds))
            {
                lock (this.locks)
                {
                    this.locks[lockKey] = new SemaphoreSlim(0);
                    return true;
                }
            }

            return false;
        }

        public string StringGet(string key)
        {
            this.cache.TryGetValue(key, out var result);
            return result;
        }

        public bool StringSet(string key, string value, TimeSpan? expiry)
        {
            this.cache[key] = value;
            return true;
        }
    }
}
