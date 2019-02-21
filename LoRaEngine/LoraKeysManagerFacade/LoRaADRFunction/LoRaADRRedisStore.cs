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

    public class LoRaADRRedisStore : ILoRaADRStore
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

        public async Task AddTableEntry(LoRaADRTableEntry entry)
        {
            using (var redisLock = new RedisLockWrapper(entry.DevEUI, this.redisCache))
            {
                if (redisLock.TakeLock())
                {
                    var entryKey = GetEntryKey(entry.DevEUI);
                    var table = await this.GetADRTableCore(entryKey) ?? new LoRaADRTable();

                    var existing = table.Entries.FirstOrDefault(itm => itm.FCnt == entry.FCnt);

                    if (existing == null)
                    {
                        // first for this framecount, simply add it
                        entry.GatewayCount = 1;
                        table.Entries.Add(entry);
                    }
                    else
                    {
                        if (existing.Snr < entry.Snr)
                        {
                            // better update with this entry
                            existing.Snr = entry.Snr;
                            existing.GatewayId = entry.GatewayId;
                        }

                        existing.GatewayCount++;
                    }

                    if (table.Entries.Count > LoRaADRTable.FrameCountCaptureCount)
                    {
                        table.Entries.RemoveAt(0);
                    }

                    // update redis store
                    this.redisCache.StringSet(entryKey, JsonConvert.SerializeObject(table));
                }
            }
        }

        public async Task<LoRaADRTable> GetADRTable(string devEUI)
        {
            const string lockOwner = "_ADRRedisCache";
            var entryKey = GetEntryKey(devEUI);
            var lockKey = entryKey + LockToken;

            if (this.redisCache.LockTake(lockKey, lockOwner, TimeSpan.FromSeconds(2)))
            {
                try
                {
                    return await this.GetADRTableCore(entryKey);
                }
                finally
                {
                    this.redisCache.LockRelease(lockKey, lockOwner);
                }
            }

            Logger.Log(devEUI, "Failed to acquire ADR redis lock. Can't deliver ADR Table", Microsoft.Extensions.Logging.LogLevel.Warning);
            return null;
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
