// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public sealed class LoRaDeviceCache : IDisposable
    {
        private const string CacheKeyLockSuffix = "msglock";
        private static readonly TimeSpan LockWaitingTimeout = TimeSpan.FromSeconds(10);
        private static IDatabase redisCache;

        private readonly string gatewayId;
        private readonly string devEUI;

        public bool IsLockOwner { get; private set; }

        private string lockKey;

        private LoRaDeviceCache(string devEUI, string gatewayId)
        {
            this.devEUI = devEUI;
            this.gatewayId = gatewayId;
        }

        public static LoRaDeviceCache Create(ExecutionContext context, string devEUI, string gatewayId)
        {
            if (string.IsNullOrEmpty(devEUI))
            {
                throw new ArgumentNullException("devEUI");
            }

            if (string.IsNullOrEmpty(gatewayId))
            {
                throw new ArgumentNullException("gatewayId");
            }

            EnsureRedisInstance(context);
            return new LoRaDeviceCache(devEUI, gatewayId);
        }

        public bool TryToLock()
        {
            if (this.IsLockOwner)
            {
                return true;
            }

            this.lockKey = this.devEUI + CacheKeyLockSuffix;
            if (!redisCache.LockTake(this.lockKey, this.gatewayId, LockWaitingTimeout))
            {
                return false;
            }

            this.IsLockOwner = true;
            return true;
        }

        public DeviceCacheInfo Initialize(int clientFCntDown = 0, int clientFCntUp = 0)
        {
            // it is the first message from this device
            var newFCntDown = clientFCntDown + 1;
            var serverStateForDeviceInfo = new DeviceCacheInfo
            {
                FCntDown = newFCntDown,
                FCntUp = clientFCntUp,
                GatewayId = this.gatewayId
            };

            this.StoreInfo(serverStateForDeviceInfo);
            return serverStateForDeviceInfo;
        }

        public bool TryGetInfo(out DeviceCacheInfo info)
        {
            info = null;
            if (!this.IsLockOwner)
            {
                throw new InvalidOperationException($"Trying to read information without owning the lock. Device: {this.devEUI} Gateway: {this.gatewayId}");
            }

            string cachedFCnt = redisCache.StringGet(this.devEUI, CommandFlags.DemandMaster);
            if (string.IsNullOrEmpty(cachedFCnt))
            {
                return false;
            }

            info = (DeviceCacheInfo)JsonConvert.DeserializeObject(cachedFCnt, typeof(DeviceCacheInfo));
            return info != null;
        }

        public void StoreInfo(DeviceCacheInfo info)
        {
            if (!this.IsLockOwner)
            {
                throw new InvalidOperationException($"Trying to update information without owning the lock. Device: {this.devEUI} Gateway: {this.gatewayId}");
            }

            LoRaDeviceCache.redisCache.StringSet(this.devEUI, JsonConvert.SerializeObject(info), new TimeSpan(30, 0, 0, 0), When.Always, CommandFlags.DemandMaster);
        }

        public static void Delete(string devEUI, ExecutionContext context)
        {
            if (string.IsNullOrEmpty(devEUI))
            {
                return;
            }

            EnsureRedisInstance(context);
            redisCache.KeyDelete(devEUI);
        }

        private static void EnsureRedisInstance(ExecutionContext context)
        {
            if (redisCache != null)
            {
                return;
            }

            lock (typeof(LoRaDeviceCache))
            {
                if (redisCache == null)
                {
                    var config = new ConfigurationBuilder()
                                .SetBasePath(context.FunctionAppDirectory)
                                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                                .AddEnvironmentVariables()
                                .Build();

                    var redisConnectionString = config.GetConnectionString("RedisConnectionString");

                    if (string.IsNullOrEmpty(redisConnectionString))
                    {
                        string errorMsg = "Missing RedisConnectionString in settings";
                        throw new Exception(errorMsg);
                    }

                    var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                    redisCache = redis.GetDatabase();
                }
            }
        }

        private void ReleaseLock()
        {
            if (!this.IsLockOwner)
            {
                return;
            }

            var released = LoRaDeviceCache.redisCache.LockRelease(this.lockKey, this.gatewayId);
            if (!released)
            {
                throw new InvalidOperationException("failed to release lock");
            }

            this.IsLockOwner = false;
        }

        public void Dispose()
        {
            this.ReleaseLock();
        }
    }
}
