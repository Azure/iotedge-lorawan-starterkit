// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using StackExchange.Redis;

    public class LoRaDeviceCacheRedisStore : ILoRaDeviceCacheStore
    {
        private IDatabase redisCache;

        public LoRaDeviceCacheRedisStore(IDatabase redisCache)
        {
            this.redisCache = redisCache;
        }

        public bool LockTake(string key, string value, TimeSpan timeout)
        {
            return this.redisCache.LockTake(key, value, timeout);
        }

        public string StringGet(string key)
        {
            return this.redisCache.StringGet(key, CommandFlags.DemandMaster);
        }

        public bool StringSet(string key, string value, TimeSpan? expiry, bool onlyIfNotExists = false)
        {
            var when = onlyIfNotExists ? When.NotExists : When.Always;
            return this.redisCache.StringSet(key, value, expiry, when, CommandFlags.DemandMaster);
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

        public long ListAdd(string key, string value, TimeSpan? expiry = null)
        {
            var itemCount = this.redisCache.ListRightPush(key, value);
            if (expiry.HasValue)
                this.redisCache.KeyExpire(key, expiry);

            return itemCount;
        }

        public IReadOnlyList<string> ListGet(string key)
        {
            var list = this.redisCache.ListRange(key);
            return list.Select(x => (string)x).ToList();
        }
    }
}
