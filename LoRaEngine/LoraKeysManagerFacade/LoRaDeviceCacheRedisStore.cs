// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class LoRaDeviceCacheRedisStore : ILoRaDeviceCacheStore
    {
        private readonly IDatabase redisCache;
        private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

        public LoRaDeviceCacheRedisStore(IDatabase redisCache)
        {
            this.redisCache = redisCache;
        }

        public async Task<bool> LockTakeAsync(string key, string owner, TimeSpan expiration, bool block = true)
        {
            var sw = Stopwatch.StartNew();
            bool taken;

            while (!(taken = this.redisCache.LockTake(key, owner, expiration, CommandFlags.DemandMaster)) && block)
            {
                if (sw.Elapsed > LockTimeout)
                    break;
                await Task.Delay(100);
            }

            return taken;
        }

        public string StringGet(string key)
        {
            return this.redisCache.StringGet(key, CommandFlags.DemandMaster);
        }

        public T GetObject<T>(string key)
            where T : class
        {
            var str = StringGet(key);
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(str);
            }
            catch (JsonSerializationException)
            {
                return null;
            }
        }

        public bool StringSet(string key, string value, TimeSpan? expiration, bool onlyIfNotExists = false)
        {
            var when = onlyIfNotExists ? When.NotExists : When.Always;
            return this.redisCache.StringSet(key, value, expiration, when, CommandFlags.DemandMaster);
        }

        public async Task<TimeSpan?> GetObjectTTL(string key) => await this.redisCache.KeyTimeToLiveAsync(key);

        public bool ObjectSet<T>(string key, T value, TimeSpan? expiration, bool onlyIfNotExists = false)
            where T : class
        {
            var str = value != null ? JsonConvert.SerializeObject(value) : null;
            return StringSet(key, str, expiration, onlyIfNotExists);
        }

        public bool KeyExists(string key)
        {
            return this.redisCache.KeyExists(key, CommandFlags.DemandMaster);
        }

        public bool KeyDelete(string key)
        {
            return this.redisCache.KeyDelete(key, CommandFlags.DemandMaster);
        }

        public bool LockRelease(string key, string value)
        {
            return this.redisCache.LockRelease(key, value);
        }

        public long ListAdd(string key, string value, TimeSpan? expiration = null)
        {
            var itemCount = this.redisCache.ListRightPush(key, value);
            if (expiration.HasValue)
                _ = this.redisCache.KeyExpire(key, expiration);

            return itemCount;
        }

        public IReadOnlyList<string> ListGet(string key)
        {
            var list = this.redisCache.ListRange(key);
            return list.Select(x => (string)x).ToList();
        }

        public void SetHashObject(string key, string subkey, string value, TimeSpan? timeToExpire = null)
        {
            _ = this.redisCache.HashSet(key, subkey, value);
            if (timeToExpire.HasValue)
            {
                _ = this.redisCache.KeyExpire(key, DateTime.UtcNow.Add(timeToExpire.Value));
            }
        }

        public HashEntry[] GetHashObject(string key)
        {
            return this.redisCache.HashGetAll(key);
        }

        public void ReplaceHashObjects<T>(string cacheKey, IDictionary<string, T> input, TimeSpan? timeToExpire = null, bool removeOldOccurence = false)
            where T : class
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            if (removeOldOccurence)
            {
                _ = this.redisCache.KeyDelete(cacheKey);
            }

            var hashEntries = new HashEntry[input.Count];
            var i = 0;
            foreach (var element in input)
            {
                hashEntries[i] = new HashEntry(element.Key, JsonConvert.SerializeObject(element.Value));
                i++;
            }

            this.redisCache.HashSet(cacheKey, hashEntries, CommandFlags.DemandMaster);
            if (timeToExpire.HasValue)
            {
                _ = this.redisCache.KeyExpire(cacheKey, DateTime.UtcNow.Add(timeToExpire.Value));
            }
        }

        public bool TryChangeLockTTL(string key, TimeSpan timeToExpire)
        {
            return this.redisCache.KeyExpire(key, DateTime.UtcNow.Add(timeToExpire));
        }
    }
}
