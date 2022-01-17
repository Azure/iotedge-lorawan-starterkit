// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class LoRaADRRedisStore : LoRaADRStoreBase, ILoRaADRStore
    {
        private const string CacheToken = ":ADR";
        private const string LockToken = ":lock";
        private readonly IDatabase redisCache;
        private readonly ILogger<LoRaADRRedisStore> logger;

        private sealed class RedisLockWrapper : IDisposable
        {
            private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);
            private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(15);
            private readonly string lockKey;
            private readonly string owner;
            private readonly IDatabase redisCache;
            private bool ownsLock;

            internal RedisLockWrapper(DevEui devEUI, IDatabase redisCache, string owner = ":LoRaRedisStore")
            {
                this.lockKey = GetEntryKey(devEUI) + LockToken;
                this.redisCache = redisCache;
                this.owner = owner;
            }

            internal async Task<bool> TakeLockAsync()
            {
                var start = DateTime.UtcNow;
                while (!(this.ownsLock = await this.redisCache.LockTakeAsync(this.lockKey, this.owner, LockDuration)))
                {
                    if (DateTime.UtcNow - start > LockTimeout)
                        break;
                    await Task.Delay(100);
                }

                return this.ownsLock;
            }

            public void Dispose()
            {
                if (this.ownsLock)
                {
                    _ = this.redisCache.LockRelease(this.lockKey, this.owner);
                    this.ownsLock = false;
                }
            }
        }

        public LoRaADRRedisStore(IDatabase redisCache, ILogger<LoRaADRRedisStore> logger)
        {
            this.redisCache = redisCache;
            this.logger = logger;
        }

        public async Task UpdateADRTable(DevEui devEUI, LoRaADRTable table)
        {
            using var redisLock = new RedisLockWrapper(devEUI, this.redisCache);
            if (await redisLock.TakeLockAsync())
            {
                _ = await this.redisCache.StringSetAsync(GetEntryKey(devEUI), JsonConvert.SerializeObject(table));
            }
        }

        public async Task<LoRaADRTable> AddTableEntry(LoRaADRTableEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            LoRaADRTable table = null;
            using (var redisLock = new RedisLockWrapper(entry.DevEUI, this.redisCache))
            {
                if (await redisLock.TakeLockAsync())
                {
                    var entryKey = GetEntryKey(entry.DevEUI);
                    table = await GetADRTableCore(entryKey) ?? new LoRaADRTable();

                    AddEntryToTable(table, entry);

                    // update redis store
                    _ = await this.redisCache.StringSetAsync(entryKey, JsonConvert.SerializeObject(table));
                }
            }

            return table;
        }

        public async Task<LoRaADRTable> GetADRTable(DevEui devEUI)
        {
            using (var redisLock = new RedisLockWrapper(devEUI, this.redisCache))
            {
                if (await redisLock.TakeLockAsync())
                {
                    return await GetADRTableCore(GetEntryKey(devEUI));
                }
            }

            this.logger.LogError("Failed to acquire ADR redis lock. Can't deliver ADR Table");
            return null;
        }

        public async Task<bool> Reset(DevEui devEUI)
        {
            using (var redisLock = new RedisLockWrapper(devEUI, this.redisCache))
            {
                if (await redisLock.TakeLockAsync())
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

        private static string GetEntryKey(DevEui devEUI)
        {
            return devEUI + CacheToken;
        }
    }
}
