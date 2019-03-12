﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    internal class LoRaInMemoryDeviceStore : ILoRaDeviceCacheStore
    {
        private readonly Dictionary<string, object> cache;
        private readonly Dictionary<string, SemaphoreSlim> locks;

        public LoRaInMemoryDeviceStore()
        {
            this.cache = new Dictionary<string, object>();
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
                if (this.locks.TryGetValue(key, out var sem))
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

            lock (this.locks)
            {
                if (!this.locks.TryGetValue(key, out waiter))
                {
                    this.locks[key] = new SemaphoreSlim(0);
                    return true;
                }
            }

            if (waiter.Wait((int)timeout.TotalMilliseconds))
            {
                lock (this.locks)
                {
                    this.locks[key] = new SemaphoreSlim(0);
                    return true;
                }
            }

            return false;
        }

        public string StringGet(string key)
        {
            this.cache.TryGetValue(key, out var result);
            return result as string;
        }

        public bool StringSet(string key, string value, TimeSpan? expiry, bool onlyIfNotExists)
        {
            if (onlyIfNotExists)
            {
                return this.cache.TryAdd(key, value);
            }

            this.cache[key] = value;
            return true;
        }

        public long ListAdd(string key, string value, TimeSpan? expiry = null)
        {
            this.cache.TryAdd(key, new List<string>());
            var list = this.cache[key] as List<string>;
            list.Add(value);

            return list.Count;
        }

        public IReadOnlyList<string> ListGet(string key)
        {
            if (this.cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue as List<string>;
            }

            return null;
        }
    }
}
