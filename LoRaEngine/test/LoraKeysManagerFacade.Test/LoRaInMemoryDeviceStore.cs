// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    internal class LoRaInMemoryDeviceStore : ILoRaDeviceCacheStore
    {
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(60);
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
                    return true;
                }
            }
        }

        public async Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration, bool block = true)
        {
            SemaphoreSlim waiter = null;
            lock (this.locks)
            {
                if (!this.locks.TryGetValue(key, out waiter))
                {
                    this.locks[key] = new SemaphoreSlim(0, 1);
                    return true;
                }
            }

            if (block && await waiter.WaitAsync((int)LockTimeout.TotalMilliseconds))
            {
                return true;
            }

            return false;
        }

        public string StringGet(string key)
        {
            this.cache.TryGetValue(key, out var result);
            return result as string;
        }

        public T GetObject<T>(string key)
            where T : class
        {
            var str = this.StringGet(key);
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(str);
            }
            catch
            {
                return null;
            }
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

        public bool ObjectSet<T>(string key, T value, TimeSpan? expiration, bool onlyIfNotExists = false)
            where T : class
        {
            var str = value != null ? JsonConvert.SerializeObject(value) : null;
            return this.StringSet(key, str, expiration, onlyIfNotExists);
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

        public bool KeyExists(string key)
        {
            return this.cache.ContainsKey(key);
        }

        public HashEntry[] GetHashObject(string key)
        {
            throw new NotImplementedException();
        }

        public bool TryChangeLockTTL(string key, TimeSpan timeToExpire)
        {
            throw new NotImplementedException();
        }

        public bool TrySetHashObject(string key, string subkey, string value, TimeSpan? timeToExpire = null)
        {
            throw new NotImplementedException();
        }

        public void ReplaceHashObjects<T>(string cacheKey, IDictionary<string, T> input, TimeSpan? timeToExpire = null, bool removeOldOccurence = false)
            where T : class
        {
            throw new NotImplementedException();
        }
    }
}
