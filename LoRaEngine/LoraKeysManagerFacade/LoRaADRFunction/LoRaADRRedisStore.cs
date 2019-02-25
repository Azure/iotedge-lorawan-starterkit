// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class LoRaADRRedisStore : LoRaADRStoreBase, ILoRaADRStore
    {
        const string CacheToken = "ADR";
        const string LockToken = "_lock";
        IDatabase redisCache;

        sealed class RedisLockWrapper : IDisposable
        {
            private string lockKey;
            private string owner;
            private IDatabase redisCache;
            private bool ownsLock;

            internal RedisLockWrapper(string devEUI, IDatabase redisCache, string owner = "_LoRaRedisStore")
            {
                this.lockKey = GetEntryKey(devEUI) + LockToken;
                this.redisCache = redisCache;
                this.owner = owner;
            }

            internal bool TakeLock()
            {
                return this.ownsLock = this.redisCache.LockTake(this.lockKey, this.owner, TimeSpan.FromSeconds(10));
            }

            public void Dispose()
            {
                if (this.ownsLock)
                {
                    this.redisCache.LockRelease(this.lockKey, this.owner);
                    this.ownsLock = false;
                }
            }
        }

        public LoRaADRRedisStore(string redisConnectionString)
        {
            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            this.redisCache = redis.GetDatabase();
        }

        public async Task UpdateADRTable(string devEUI, LoRaADRTable table)
        {
            using (var redisLock = new RedisLockWrapper(devEUI, this.redisCache))
            {
                if (redisLock.TakeLock())
                {
                    await this.redisCache.StringSetAsync(GetEntryKey(devEUI), JsonConvert.SerializeObject(table));
                }
            }
        }

        public async Task<LoRaADRTable> AddTableEntry(LoRaADRTableEntry entry)
        {
            LoRaADRTable table = null;
            using (var redisLock = new RedisLockWrapper(entry.DevEUI, this.redisCache))
            {
                if (redisLock.TakeLock())
                {
                    var entryKey = GetEntryKey(entry.DevEUI);
                    table = await this.GetADRTableCore(entryKey) ?? new LoRaADRTable();

                    AddEntryToTable(table, entry);

                    // update redis store
                    this.redisCache.StringSet(entryKey, JsonConvert.SerializeObject(table));
                }
            }

            return table;
        }

        public async Task<LoRaADRTable> GetADRTable(string devEUI)
        {
            using (var redisLock = new RedisLockWrapper(devEUI, this.redisCache))
            {
                if (redisLock.TakeLock())
                {
                    return await this.GetADRTableCore(GetEntryKey(devEUI));
                }
            }

            Logger.Log(devEUI, "Failed to acquire ADR redis lock. Can't deliver ADR Table", Microsoft.Extensions.Logging.LogLevel.Warning);
            return null;
        }

        public async Task<bool> Reset(string devEUI)
        {
            using (var redisLock = new RedisLockWrapper(devEUI, this.redisCache))
            {
                if (redisLock.TakeLock())
                {
                    return await this.redisCache.KeyDeleteAsync(GetEntryKey(devEUI));
                }
            }

            return false;
        }

        private async Task<LoRaADRTable> GetADRTableCore(string key)
        {
            var existingContent = await this.redisCache.StringGetAsync(key);
            if (!string.IsNullOrEmpty(existingContent))
            {
                return JsonConvert.DeserializeObject<LoRaADRTable>(existingContent);
            }

            return null;
        }

        private static string GetEntryKey(string devEUI)
        {
            return devEUI + CacheToken;
        }
    }
}
