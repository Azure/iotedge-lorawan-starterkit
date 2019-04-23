// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Newtonsoft.Json;

    public sealed class LoRaDeviceCache : IDisposable
    {
        private const string CacheKeyLockSuffix = "msglock";
        private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(10);

        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly string gatewayId;
        private readonly string devEUI;
        private readonly string cacheKey;

        public bool IsLockOwner { get; private set; }

        private string lockKey;

        public LoRaDeviceCache(ILoRaDeviceCacheStore cacheStore, string devEUI, string gatewayId)
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
            this.cacheKey = devEUI;
        }

        public async Task<bool> TryToLockAsync(string lockKey = null, bool block = true)
        {
            if (this.IsLockOwner)
            {
                return true;
            }

            var lk = lockKey ?? this.devEUI + CacheKeyLockSuffix;

            if (this.IsLockOwner = await this.cacheStore.LockTakeAsync(lk, this.gatewayId, LockExpiry, block))
            {
                // store the used key
                this.lockKey = lk;
            }

            return this.IsLockOwner;
        }

        public bool Initialize(uint fCntUp = 0, uint fCntDown = 0)
        {
            // it is the first message from this device
            var serverStateForDeviceInfo = new DeviceCacheInfo
            {
                FCntDown = fCntDown,
                FCntUp = fCntUp,
                GatewayId = this.gatewayId
            };

            return this.StoreInfo(serverStateForDeviceInfo, true);
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

            info = this.cacheStore.GetObject<DeviceCacheInfo>(this.cacheKey);
            return info != null;
        }

        public bool StoreInfo(DeviceCacheInfo info, bool initialize = false)
        {
            this.EnsureLockOwner();
            return this.cacheStore.StringSet(this.cacheKey, JsonConvert.SerializeObject(info), new TimeSpan(1, 0, 0, 0), initialize);
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
