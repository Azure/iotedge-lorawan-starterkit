// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using Microsoft.Azure.WebJobs;
    using Newtonsoft.Json;

    public sealed class LoRaDeviceCache : IDisposable
    {
        private const string CacheKeyLockSuffix = "msglock";
        private static readonly TimeSpan LockWaitingTimeout = TimeSpan.FromSeconds(10);
        private static ILoRaDeviceCacheStore cacheStore;
        private static object cacheSingletonLock = new object();

        private readonly string gatewayId;
        private readonly string devEUI;
        private readonly string cacheKey;

        /// <summary>
        /// Setting an explicit device store cache implementation
        /// </summary>
        /// <param name="cacheStore">The custom store to use</param>
        /// <remarks>Do only use for unit testing</remarks>
        public static void EnsureCacheStore(ILoRaDeviceCacheStore cacheStore)
        {
            lock (cacheSingletonLock)
            {
                if (LoRaDeviceCache.cacheStore == null)
                {
                    LoRaDeviceCache.cacheStore = cacheStore;
                }
            }
        }

        public bool IsLockOwner { get; private set; }

        private string lockKey;

        private LoRaDeviceCache(string devEUI, string gatewayId, string cacheKey)
        {
            this.devEUI = devEUI;
            this.gatewayId = gatewayId;
            this.cacheKey = cacheKey ?? devEUI;
        }

        public static LoRaDeviceCache Create(string functionAppDirectory, string devEUI, string gatewayId, string cacheKey = null)
        {
            if (string.IsNullOrEmpty(devEUI))
            {
                throw new ArgumentNullException("devEUI");
            }

            if (string.IsNullOrEmpty(gatewayId))
            {
                throw new ArgumentNullException("gatewayId");
            }

            EnsureCacheStore(functionAppDirectory);
            return new LoRaDeviceCache(devEUI, gatewayId, cacheKey);
        }

        public bool TryToLock(string lockKey = null)
        {
            if (this.IsLockOwner)
            {
                return true;
            }

            this.lockKey = lockKey ?? this.devEUI + CacheKeyLockSuffix;
            if (!cacheStore.LockTake(this.lockKey, this.gatewayId, LockWaitingTimeout))
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

        public bool TryGetValue(out string value)
        {
            value = null;
            this.EnsureLockOwner();
            value = cacheStore.StringGet(this.cacheKey);
            return value != null;
        }

        public bool TryGetInfo(out DeviceCacheInfo info)
        {
            info = null;
            this.EnsureLockOwner();

            string cachedFCnt = cacheStore.StringGet(this.cacheKey);
            if (string.IsNullOrEmpty(cachedFCnt))
            {
                return false;
            }

            info = JsonConvert.DeserializeObject<DeviceCacheInfo>(cachedFCnt);
            return info != null;
        }

        public void StoreInfo(DeviceCacheInfo info)
        {
            this.EnsureLockOwner();
            cacheStore.StringSet(this.cacheKey, JsonConvert.SerializeObject(info), new TimeSpan(30, 0, 0, 0));
        }

        public void SetValue(string value, TimeSpan? expiry = null)
        {
            this.EnsureLockOwner();
            if (!expiry.HasValue)
            {
                expiry = TimeSpan.FromMinutes(1);
            }

            cacheStore.StringSet(this.cacheKey, value, expiry);
        }

        public static void Delete(string key, string functionAppDirectory)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            EnsureCacheStore(functionAppDirectory);
            cacheStore.KeyDelete(key);
        }

        private void EnsureLockOwner()
        {
            if (!this.IsLockOwner)
            {
                throw new InvalidOperationException($"Trying to access cache without owning the lock. Device: {this.devEUI} Gateway: {this.gatewayId}");
            }
        }

        private static void EnsureCacheStore(string functionAppDirectory)
        {
            if (cacheStore != null)
            {
                return;
            }

            lock (cacheSingletonLock)
            {
                if (cacheStore == null)
                {
                    cacheStore = new LoRaDeviceCacheRedisStore(functionAppDirectory);
                }
            }
        }

        private void ReleaseLock()
        {
            if (!this.IsLockOwner)
            {
                return;
            }

            var released = cacheStore.LockRelease(this.lockKey, this.gatewayId);
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
