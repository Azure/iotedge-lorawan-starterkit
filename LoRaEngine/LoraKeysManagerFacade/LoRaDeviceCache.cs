// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using Newtonsoft.Json;

    public sealed class LoRaDeviceCache : IDisposable
    {
        private const string CacheKeyLockSuffix = "msglock";
        private static readonly TimeSpan LockWaitingTimeout = TimeSpan.FromSeconds(10);

        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly string gatewayId;
        private readonly string devEUI;
        private readonly string cacheKey;

        public bool IsLockOwner { get; private set; }

        private string lockKey;

        public LoRaDeviceCache(ILoRaDeviceCacheStore cacheStore, string devEUI, string gatewayId, string cacheKey = null)
        {
            if (string.IsNullOrEmpty(devEUI))
            {
                throw new ArgumentNullException("devEUI");
            }

            if (string.IsNullOrEmpty(gatewayId))
            {
                throw new ArgumentNullException("gatewayId");
            }

            this.cacheStore = cacheStore;
            this.devEUI = devEUI;
            this.gatewayId = gatewayId;
            this.cacheKey = cacheKey ?? devEUI;
        }

        public bool TryToLock(string lockKey = null)
        {
            if (this.IsLockOwner)
            {
                return true;
            }

            this.lockKey = lockKey ?? this.devEUI + CacheKeyLockSuffix;
            if (!this.cacheStore.LockTake(this.lockKey, this.gatewayId, LockWaitingTimeout))
            {
                return false;
            }

            this.IsLockOwner = true;
            return true;
        }

        public DeviceCacheInfo Initialize(uint fCntUp = 0, uint fCntDown = 0)
        {
            // it is the first message from this device
            var serverStateForDeviceInfo = new DeviceCacheInfo
            {
                FCntDown = fCntDown,
                FCntUp = fCntUp,
                GatewayId = this.gatewayId
            };

            this.StoreInfo(serverStateForDeviceInfo);
            return serverStateForDeviceInfo;
        }

        public bool TryGetValue(out string value)
        {
            value = null;
            this.EnsureLockOwner();
            value = this.cacheStore.StringGet(this.cacheKey);
            return value != null;
        }

        public bool Exists()
        {
            return this.cacheStore.KeyExists(this.cacheKey);
        }

        public bool HasValue()
        {
            return this.cacheStore.StringGet(this.cacheKey) != null;
        }

        public bool TryGetInfo(out DeviceCacheInfo info)
        {
            info = null;
            this.EnsureLockOwner();

            string cachedFCnt = this.cacheStore.StringGet(this.cacheKey);
            if (string.IsNullOrEmpty(cachedFCnt))
            {
                return false;
            }

            info = JsonConvert.DeserializeObject<DeviceCacheInfo>(cachedFCnt);
            return info != null;
        }

        public bool StoreInfo(DeviceCacheInfo info)
        {
            this.EnsureLockOwner();
            return this.cacheStore.StringSet(this.cacheKey, JsonConvert.SerializeObject(info), new TimeSpan(30, 0, 0, 0));
        }

        public void SetValue(string value, TimeSpan? expiry = null)
        {
            this.EnsureLockOwner();
            if (!expiry.HasValue)
            {
                expiry = TimeSpan.FromMinutes(1);
            }

            this.cacheStore.StringSet(this.cacheKey, value, expiry);
        }

        public void ClearCache()
        {
            this.EnsureLockOwner();
            this.cacheStore.KeyDelete(this.cacheKey);
        }

        private void EnsureLockOwner()
        {
            if (!this.IsLockOwner)
            {
                throw new InvalidOperationException($"Trying to access cache without owning the lock. Device: {this.devEUI} Gateway: {this.gatewayId}");
            }
        }

        private void ReleaseLock()
        {
            if (!this.IsLockOwner)
            {
                return;
            }

            var released = this.cacheStore.LockRelease(this.lockKey, this.gatewayId);
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
