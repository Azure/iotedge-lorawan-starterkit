// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public sealed class LoRaDevAddrCache
    {
        private const string DeltaUpdateKey = "deltaUpdateKey";
        private const string LastDeltaUpdateKeyValue = "lastDeltaUpdateKeyValue";
        private const string FullUpdateKey = "fullUpdateKey";
        private const string UpdatingDevAddrCacheLock = "globalUpdateKey";
        private const string CacheKeyPrefix = "devAddrTable:";
        private const string DevAddrLockName = "devAddrLock:";
        public static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan FullUpdateKeyTimeSpan = TimeSpan.FromHours(25);
        private static readonly TimeSpan DeltaUpdateKeyTimeSpan = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan UpdatingDevAddrCacheLockTimeSpan = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan DevAddrObjectsTTL = TimeSpan.FromHours(24);

        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly ILogger logger;
        private readonly string lockOwner;

        private static string GenerateKey(string devAddr) => CacheKeyPrefix + devAddr;

        public LoRaDevAddrCache(ILoRaDeviceCacheStore cacheStore, RegistryManager registryManager, ILogger logger, string gatewayId)
        {
            this.cacheStore = cacheStore;
            this.logger = logger ?? NullLogger.Instance;
            this.lockOwner = gatewayId ?? Guid.NewGuid().ToString();

            // perform the necessary syncs
            _ = this.PerformNeededSyncs(registryManager);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaDevAddrCache"/> class.
        /// This constructor is only used by the DI Manager. Use the other constructore for general usage.
        /// </summary>
        public LoRaDevAddrCache(ILoRaDeviceCacheStore cacheStore, ILogger logger, string gatewayId)
        {
            this.cacheStore = cacheStore;
            this.logger = logger ?? NullLogger.Instance;
            this.lockOwner = gatewayId ?? Guid.NewGuid().ToString();
        }

        public bool TryGetInfo(string devAddr, out List<DevAddrCacheInfo> info)
        {
            var tmp = this.cacheStore.GetHashObject(GenerateKey(devAddr));
            if (tmp?.Length > 0)
            {
                info = new List<DevAddrCacheInfo>(tmp.Length);
                foreach (var tm in tmp)
                {
                    info.Add(JsonConvert.DeserializeObject<DevAddrCacheInfo>(tm.Value));
                }
            }
            else
            {
                info = null;
            }

            return info?.Count > 0;
        }

        public bool StoreInfo(DevAddrCacheInfo info, bool initialize = false)
        {
            if (info == null)
            {
                throw new ArgumentNullException("Required DevAddrCacheInfo argument was null");
            }

            bool success = false;
            var serializedObjectValue = JsonConvert.SerializeObject(info);

            string cacheKeyToUse = GenerateKey(info.DevAddr);

            success = this.cacheStore.TrySetHashObject(cacheKeyToUse, info.DevEUI, serializedObjectValue);

            if (success)
            {
                this.logger.LogInformation($"Successfully saved dev address info on dictionary key: {cacheKeyToUse}, hashkey: {info.DevEUI}, object: {serializedObjectValue}");
            }
            else
            {
                this.logger.LogError($"Failure to save dev address info on dictionary key: {cacheKeyToUse}, hashkey: {info?.DevEUI}, object: {serializedObjectValue}");
            }

            return success;
        }

        internal async Task PerformNeededSyncs(RegistryManager registryManager)
        {
            // If a full update is expected
            if (await this.cacheStore.LockTakeAsync(FullUpdateKey, this.lockOwner, FullUpdateKeyTimeSpan, block: false))
            {
                var ownGlobalLock = false;
                var fullUpdatePerformed = false;
                try
                {
                    // if a full update is needed I take the global lock and perform a full reload
                    if (!await this.cacheStore.LockTakeAsync(UpdatingDevAddrCacheLock, this.lockOwner, UpdatingDevAddrCacheLockTimeSpan, block: true))
                    {
                        this.logger.LogDebug("A full reload was needed but failed to acquire global update lock");
                    }
                    else
                    {
                        ownGlobalLock = true;
                        await this.PerformFullReload(registryManager);
                        this.logger.LogDebug("A full reload was completed");
                        // if successfull i set the delta lock to 5 minutes and release the global lock
                        this.cacheStore.StringSet(FullUpdateKey, this.lockOwner, FullUpdateKeyTimeSpan);
                        this.cacheStore.StringSet(DeltaUpdateKey, this.lockOwner, DeltaUpdateKeyTimeSpan);
                        fullUpdatePerformed = true;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"Exception occured during dev addresses full reload {ex.ToString()}");
                }
                finally
                {
                    if (ownGlobalLock)
                    {
                        this.cacheStore.LockRelease(UpdatingDevAddrCacheLock, this.lockOwner);
                    }

                    if (!fullUpdatePerformed)
                    {
                        if (!this.cacheStore.TryChangeLockTTL(FullUpdateKey, timeToExpire: TimeSpan.FromMinutes(1)))
                        {
                            this.logger.LogError("Could not change the TTL of the lock");
                        }
                    }
                }
            }
            else if (await this.cacheStore.LockTakeAsync(UpdatingDevAddrCacheLock, this.lockOwner, TimeSpan.FromMinutes(5), block: false))
            {
                try
                {
                    if (await this.cacheStore.LockTakeAsync(DeltaUpdateKey, this.lockOwner, TimeSpan.FromMinutes(5)))
                    {
                        await this.PerformDeltaReload(registryManager);
                        this.logger.LogDebug("A delta reload was completed");
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"Exception occured during dev addresses delta reload {ex.ToString()}");
                }
                finally
                {
                    this.cacheStore.LockRelease(UpdatingDevAddrCacheLock, this.lockOwner);
                }
            }
        }

        /// <summary>
        /// Perform a full relaoad on the dev address cache. This occur typically once every 24 h.
        /// </summary>
        private async Task PerformFullReload(RegistryManager registryManager)
        {
            var query = $"SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey) OR is_defined(properties.desired.NwkSKey)";
            var devAddrCacheInfos = await this.GetDeviceTwinsFromIotHub(registryManager, query);
            this.BulkSaveDevAddrCache(devAddrCacheInfos, true);
        }

        /// <summary>
        /// Method performing a deltaReload. Typically occur every 5 minutes.
        /// </summary>
        private async Task PerformDeltaReload(RegistryManager registryManager)
        {
            // if the value is null (first call), we take five minutes before this call
            var lastUpdate = this.cacheStore.StringGet(LastDeltaUpdateKeyValue) ?? DateTime.UtcNow.AddMinutes(-5).ToString();
            var query = $"SELECT * FROM c where properties.desired.$metadata.$lastUpdated >= '{lastUpdate}' OR properties.reported.$metadata.DevAddr.$lastUpdated >= '{lastUpdate}'";
            var devAddrCacheInfos = await this.GetDeviceTwinsFromIotHub(registryManager, query);
            this.BulkSaveDevAddrCache(devAddrCacheInfos, false);
        }

        private async Task<List<DevAddrCacheInfo>> GetDeviceTwinsFromIotHub(RegistryManager registryManager, string inputQuery)
        {
            var query = registryManager.CreateQuery(inputQuery);
            this.cacheStore.StringSet(LastDeltaUpdateKeyValue, DateTime.UtcNow.ToString(), TimeSpan.FromDays(1));
            var devAddrCacheInfos = new List<DevAddrCacheInfo>();
            while (query.HasMoreResults)
            {
                var page = await query.GetNextAsTwinAsync();

                foreach (var twin in page)
                {
                    if (twin.DeviceId != null)
                    {
                        string currentDevAddr;
                        if (twin.Properties.Desired.Contains("DevAddr"))
                        {
                            currentDevAddr = twin.Properties.Desired["DevAddr"].Value as string;
                        }
                        else if (twin.Properties.Reported.Contains("DevAddr"))
                        {
                            currentDevAddr = twin.Properties.Reported["DevAddr"].Value as string;
                        }
                        else
                        {
                            continue;
                        }

                        devAddrCacheInfos.Add(new DevAddrCacheInfo()
                        {
                            DevAddr = currentDevAddr,
                            DevEUI = twin.DeviceId,
                            GatewayId = twin.Properties.Desired.Contains("GatewayId") ? twin.Properties.Desired["GatewayId"].Value as string : string.Empty,
                            LastUpdatedTwins = twin.Properties.Desired.GetLastUpdated()
                        });
                    }
                }
            }

            return devAddrCacheInfos;
        }

        /// <summary>
        /// Method to bulk save a devAddrCacheInfo list in redis in a call per devAddr
        /// </summary>
        /// <param name="canDeleteDeviceWithDevAddr"> Should delete all other elements non present in this list?</param>
        private void BulkSaveDevAddrCache(List<DevAddrCacheInfo> devAddrCacheInfos, bool canDeleteDeviceWithDevAddr)
        {
            // elements will naturally expire we only need to add new ones
            var regrouping = devAddrCacheInfos.GroupBy(x => x.DevAddr);
            foreach (var elementPerDevAddr in regrouping)
            {
                var cacheKey = GenerateKey(elementPerDevAddr.Key);
                var currentDevAddrEntry = this.cacheStore.GetHashObject(cacheKey);
                var devicesByDevEui = this.KeepExistingCacheInformation(currentDevAddrEntry, elementPerDevAddr, canDeleteDeviceWithDevAddr);
                if (devicesByDevEui != null)
                {
                    this.cacheStore.ReplaceHashObjects(cacheKey, devicesByDevEui, DevAddrObjectsTTL, canDeleteDeviceWithDevAddr);
                }
            }
        }

        /// <summary>
        /// Method to make sure we keep information currently available in the cache and we don't perform unnessecary updates.
        /// </summary>
        private IDictionary<string, DevAddrCacheInfo> KeepExistingCacheInformation(HashEntry[] cacheDevEUIEntry, IGrouping<string, DevAddrCacheInfo> newDevEUIList, bool canDeleteExistingDevice)
        {
            // if the new value are not different we want to ensure we don't save, to not update the TTL of the item.
            var toSyncValues = newDevEUIList.ToDictionary(x => x.DevEUI);
            var cacheValues = new Dictionary<string, DevAddrCacheInfo>();

            // If nothing is in the cache we want to return the new values.
            if (cacheDevEUIEntry.Length == 0)
            {
                return toSyncValues;
            }

            foreach (var devEUIEntry in cacheDevEUIEntry)
            {
                cacheValues.Add(devEUIEntry.Name, JsonConvert.DeserializeObject<DevAddrCacheInfo>(devEUIEntry.Value));
            }

            // if we can delete existing devices in the devadr cache, we take the new list as base, otherwise we take the old one.
            if (canDeleteExistingDevice)
            {
                return this.MergeOldAndNewChanges(toSyncValues, cacheValues, canDeleteExistingDevice);
            }
            else
            {
                return this.MergeOldAndNewChanges(cacheValues, toSyncValues, canDeleteExistingDevice);
            }
        }

        /// <summary>
        /// In the end we simply need to update the gateway and the Primary key. The DEVEUI and DevAddr can't be updated.
        /// </summary>
        private IDictionary<string, DevAddrCacheInfo> MergeOldAndNewChanges(IDictionary<string, DevAddrCacheInfo> valueArrayBase, IDictionary<string, DevAddrCacheInfo> valueArrayimport, bool shouldImportFromNewValues)
        {
            bool isSaveRequired = false;
            foreach (var baseValue in valueArrayBase)
            {
                if (valueArrayimport.TryGetValue(baseValue.Key, out var importValue))
                {
                    // if the item is different we need to trigger a save of the object
                    isSaveRequired = baseValue.Value != importValue;

                    if (!shouldImportFromNewValues)
                    {
                        // In this case (delta update). We are taking old value as base. We want to make sure to update the gateway Id as this is the only parameter that could change.
                        baseValue.Value.GatewayId = importValue.GatewayId;
                    }

                    // If the twins were not updated I want to make sure I keep the key, otherwise the key might have changed or the device recreated so we need to recreate it.
                    if (importValue.LastUpdatedTwins.ToLongTimeString() == baseValue.Value.LastUpdatedTwins.ToLongTimeString())
                    {
                        if (string.IsNullOrEmpty(baseValue.Value.PrimaryKey))
                        {
                            baseValue.Value.PrimaryKey = importValue.PrimaryKey;
                        }
                    }
                    else
                    {
                        baseValue.Value.PrimaryKey = string.Empty;
                    }

                    // I remove the key from the import to be able to import any delta element later.
                    valueArrayimport.Remove(baseValue.Key);
                }
                else
                {
                    // there is an additional item, we need to save
                    isSaveRequired = true;
                }
            }

            if (!shouldImportFromNewValues)
            {
                // In this case we want to make sure we import any new value that were not contained in the old cache information
                foreach (var remainingElementToImport in valueArrayimport)
                {
                    valueArrayBase.Add(remainingElementToImport.Value.DevEUI, remainingElementToImport.Value);
                }
            }

            // If no changes are required we return null to avoid saving and updating the expiry on the cache.
            return isSaveRequired ? valueArrayBase : null;
        }

        /// <summary>
        /// Method to take a lock when querying IoT Hub for a primary key.
        /// It is blocking as only one should access it.
        /// </summary>
        public async Task<bool> TryTakeDevAddrUpdateLock(string devEUI)
        {
            return await this.cacheStore.LockTakeAsync(string.Concat(DevAddrLockName, devEUI), this.lockOwner, LockExpiry, block: true);
        }

        public bool ReleaseDevAddrUpdateLock(string devEUI)
        {
            return this.cacheStore.LockRelease(string.Concat(DevAddrLockName, devEUI), this.lockOwner);
        }
    }
}