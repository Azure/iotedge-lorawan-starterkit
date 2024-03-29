// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public sealed class LoRaDevAddrCache
    {
        /// <summary>
        /// The value for this key contains the most recent twin update collected after a delta update.
        /// </summary>
        internal const string LastDeltaUpdateKeyValue = "lastDeltaUpdateKeyValue";

        /// <summary>
        /// This is the lock controlling a complete update of the cache.
        /// Complete updates of the cache are scheduled to happen every <see cref="FullUpdateKeyTimeSpan"/>.
        /// The lock is used to set the TTL to that value, so it can only be taken once for that time span.
        /// </summary>
        private const string FullUpdateLockKey = "fullUpdateKey";
        private static readonly TimeSpan FullUpdateKeyTimeSpan = TimeSpan.FromHours(24);

        /// <summary>
        /// Individual entries / hashes per dev address are made valid at least 1h longer than
        /// the full update scheduled trigger. This avoids, invalidating the cache before we
        /// re-populate it.
        /// </summary>
        private static readonly TimeSpan DevAddrObjectsTTL = FullUpdateKeyTimeSpan + TimeSpan.FromHours(1);

        /// <summary>
        /// All changes no matter if they are full or incremental, will have to acquire this lock
        /// </summary>
        private const string UpdatingDevAddrCacheLock = "globalUpdateKey";

        /// <summary>
        /// This is the time we hold onto the update lock. This should be long enough for an incremental
        /// upate to pass.
        /// </summary>
        private static readonly TimeSpan UpdatingDevAddrCacheLockTimeSpan = TimeSpan.FromMinutes(5);

        private const string CacheKeyPrefix = "devAddrTable:";
        private const string DevAddrLockName = "devAddrLock:";
        public static readonly TimeSpan DefaultSingleLockExpiry = TimeSpan.FromSeconds(10);

        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly ILogger logger;
        private readonly string lockOwner;

        private static string GenerateKey(DevAddr devAddr) => CacheKeyPrefix + devAddr;

        public LoRaDevAddrCache(ILoRaDeviceCacheStore cacheStore, IDeviceRegistryManager registryManager, ILogger logger, string gatewayId)
        {
            this.cacheStore = cacheStore;
            this.logger = logger ?? NullLogger.Instance;
            this.lockOwner = gatewayId ?? Guid.NewGuid().ToString();

            // perform the necessary syncs
            _ = PerformNeededSyncs(registryManager);
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

        public bool TryGetInfo(DevAddr devAddr, out IList<DevAddrCacheInfo> info)
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

        public void StoreInfo(DevAddrCacheInfo info)
        {
            if (info is null) throw new ArgumentNullException(nameof(info));

            var serializedObjectValue = JsonConvert.SerializeObject(info);

            var cacheKeyToUse = GenerateKey(info.DevAddr);
            var subKey = info.DevEUI is { } someDevEui ? someDevEui.ToString() : string.Empty;

            this.cacheStore.SetHashObject(cacheKeyToUse, subKey, serializedObjectValue);
            this.logger.LogInformation($"Successfully saved dev address info on dictionary key: {cacheKeyToUse}, hashkey: {info.DevEUI}, object: {serializedObjectValue}");
        }

        internal async Task PerformNeededSyncs(IDeviceRegistryManager registryManager)
        {
            // If a full update is expected
            if (await this.cacheStore.LockTakeAsync(FullUpdateLockKey, this.lockOwner, FullUpdateKeyTimeSpan, block: false))
            {
                var ownsUpdateLock = false;
                var fullUpdatePerformed = false;
                try
                {
                    // if a full update is needed we take the global lock and perform a full reload
                    if (ownsUpdateLock = await this.cacheStore.LockTakeAsync(UpdatingDevAddrCacheLock, this.lockOwner, UpdatingDevAddrCacheLockTimeSpan, block: true))
                    {
                        this.logger.LogDebug("A full reload was started");
                        await PerformFullReload(registryManager);
                        this.logger.LogDebug("A full reload was completed");
                        // we updated the full cache, we want to delay the next update to the time FullUpdateKeyTimeSpan
                        // and only process incremental updates for that time.
                        _ = this.cacheStore.TryChangeLockTTL(FullUpdateLockKey, FullUpdateKeyTimeSpan);
                        fullUpdatePerformed = true;
                    }
                    else
                    {
                        this.logger.LogDebug("A full reload was needed but failed to acquire global update lock");
                    }
                }
                catch (RedisException ex)
                {
                    this.logger.LogError($"Exception occured during dev addresses full reload {ex}");
                }
                finally
                {
                    if (ownsUpdateLock)
                    {
                        // potentially, if an incremental update comes in right after this,
                        // we would be doing an incremental update to soon. We could delay that
                        // for the time we run the incremental updates, but that could delay it
                        // longer than what we may want.
                        _ = this.cacheStore.LockRelease(UpdatingDevAddrCacheLock, this.lockOwner);
                    }

                    if (!fullUpdatePerformed)
                    {
                        if (!this.cacheStore.TryChangeLockTTL(FullUpdateLockKey, timeToExpire: TimeSpan.FromMinutes(1)))
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
                    this.logger.LogDebug("A delta reload was started");
                    await PerformDeltaReload(registryManager);
                    this.logger.LogDebug("A delta reload was completed");
                }
                catch (RedisException ex)
                {
                    this.logger.LogError($"Exception occured during dev addresses delta reload while interacting with Redis {ex}");
                }
                catch (IotHubCommunicationException ex)
                {
                    this.logger.LogError($"Exception occured during dev addresses delta reload during communication with the iot hub {ex}");
                }
                catch (IotHubException ex)
                {
                    this.logger.LogError($"An unknown IoT Hub exception occured during dev addresses delta reload  {ex}");
                }
                finally
                {
                    _ = this.cacheStore.LockRelease(UpdatingDevAddrCacheLock, this.lockOwner);
                }
            }
        }

        public async Task<TimeSpan?> GetTTLInformation(string key) => await this.cacheStore.GetObjectTTL(key);

        /// <summary>
        /// Perform a full relaoad on the dev address cache. This occur typically once every 24 h.
        /// </summary>
        private async Task PerformFullReload(IDeviceRegistryManager registryManager)
        {
            var query = registryManager.GetAllLoRaDevices();
            var devAddrCacheInfos = await GetDeviceTwinsFromIotHub(query, null);
            BulkSaveDevAddrCache(devAddrCacheInfos, true);
        }

        /// <summary>
        /// Method performing a deltaReload. Typically occur every 5 minutes.
        ///
        /// Please be aware that changes to twin are delayed in IoT Hub queries.
        /// The Delta reload is keeping track of the most recent update and using that timestamp
        /// as the start date/time for the next iteration.
        /// No one is guaranteeing that the twin changes are propagated all together and in order,
        /// therefore there could be a chance where we are missing some items.
        /// At the same time, the LoRaWanNetworkServer is proactively storing the changes in Redis
        /// after a successful join, therefore the chance of missing items should be very very low.
        /// </summary>
        private async Task PerformDeltaReload(IDeviceRegistryManager registryManager)
        {
            // if the value is null (first call), we take updates from one hour before this call
            var lastUpdate = long.TryParse(this.cacheStore.StringGet(LastDeltaUpdateKeyValue), out var cachedTicks) ? cachedTicks : DateTime.UtcNow.AddHours(-1).Ticks;
            var lastUpdateDateTime = new DateTime(lastUpdate, DateTimeKind.Utc);
            var query = registryManager.GetLastUpdatedLoRaDevices(lastUpdateDateTime);
            var devAddrCacheInfos = await GetDeviceTwinsFromIotHub(query, lastUpdate);
            BulkSaveDevAddrCache(devAddrCacheInfos, false);
        }

        private async Task<List<DevAddrCacheInfo>> GetDeviceTwinsFromIotHub(IRegistryPageResult<ILoRaDeviceTwin> query, long? lastDeltaUpdateFromCacheTicks)
        {
            var isFullReload = lastDeltaUpdateFromCacheTicks is null;
            var devAddrCacheInfos = new List<DevAddrCacheInfo>();
            while (query.HasMoreResults)
            {
                var page = await query.GetNextPageAsync();

                foreach (var twin in page.Where(twin => twin.DeviceId != null))
                {
                    if (!twin.Properties.Desired.TryRead(TwinPropertiesConstants.DevAddr, this.logger, out DevAddr devAddr) &&
                        !twin.Properties.Reported.TryRead(TwinPropertiesConstants.DevAddr, this.logger, out devAddr))
                    {
                        continue;
                    }

                    devAddrCacheInfos.Add(new DevAddrCacheInfo()
                    {
                        DevAddr = devAddr,
                        DevEUI = DevEui.Parse(twin.DeviceId),
                        GatewayId = twin.GetGatewayID(),
                        NwkSKey = twin.GetNwkSKey(),
                        LastUpdatedTwins = twin.Properties.Desired.GetLastUpdated()
                    });

                    if (!isFullReload
                        && twin.Properties.Desired.GetMetadata() is { LastUpdated: { } desiredUpdateTime }
                        && twin.Properties.Reported.GetMetadata() is { LastUpdated: { } reportedUpdateTime })
                    {
                        lastDeltaUpdateFromCacheTicks = Math.Max(lastDeltaUpdateFromCacheTicks.Value, Math.Max(desiredUpdateTime.Ticks, reportedUpdateTime.Ticks));
                    }
                }
            }

            if (!isFullReload)
            {
                _ = this.cacheStore.StringSet(LastDeltaUpdateKeyValue, lastDeltaUpdateFromCacheTicks.Value.ToString(CultureInfo.InvariantCulture), TimeSpan.FromDays(1));
            }

            return devAddrCacheInfos;
        }

        /// <summary>
        /// Method to bulk save a devAddrCacheInfo list in redis in a call per devAddr.
        /// </summary>
        /// <param name="canDeleteDeviceWithDevAddr"> Should delete all other elements non present in this list?.</param>
        private void BulkSaveDevAddrCache(List<DevAddrCacheInfo> devAddrCacheInfos, bool canDeleteDeviceWithDevAddr)
        {
            // elements will naturally expire we only need to add new ones
            var regrouping = devAddrCacheInfos.GroupBy(x => x.DevAddr);
            foreach (var elementPerDevAddr in regrouping)
            {
                var cacheKey = GenerateKey(elementPerDevAddr.Key);
                var currentDevAddrEntry = this.cacheStore.GetHashObject(cacheKey);
                var devicesByDevEui = KeepExistingCacheInformation(currentDevAddrEntry, elementPerDevAddr, canDeleteDeviceWithDevAddr);
                if (devicesByDevEui != null)
                {
                    this.cacheStore.ReplaceHashObjects(cacheKey, devicesByDevEui, DevAddrObjectsTTL, canDeleteDeviceWithDevAddr);
                }
            }
        }

        /// <summary>
        /// Method to make sure we keep information currently available in the cache and we don't perform unnessecary updates.
        /// </summary>
        private static IDictionary<string, DevAddrCacheInfo> KeepExistingCacheInformation(HashEntry[] cacheDevEUIEntry, IGrouping<DevAddr, DevAddrCacheInfo> newDevEUIList, bool canDeleteExistingDevice)
        {
            // if the new value are not different we want to ensure we don't save, to not update the TTL of the item.
            var toSyncValues = newDevEUIList.ToDictionary(x => x.DevEUI.Value.ToString());

            // If nothing is in the cache we want to return the new values.
            if (cacheDevEUIEntry.Length == 0)
            {
                return toSyncValues;
            }

            var cacheValues = new Dictionary<string, DevAddrCacheInfo>();

            foreach (var devEUIEntry in cacheDevEUIEntry)
            {
                cacheValues.Add(devEUIEntry.Name, JsonConvert.DeserializeObject<DevAddrCacheInfo>(devEUIEntry.Value));
            }

            // if we can delete existing devices in the devadr cache, we take the new list as base, otherwise we take the old one.
            if (canDeleteExistingDevice)
            {
                return MergeOldAndNewChanges(toSyncValues, cacheValues, canDeleteExistingDevice);
            }
            else
            {
                return MergeOldAndNewChanges(cacheValues, toSyncValues, canDeleteExistingDevice);
            }
        }

        /// <summary>
        /// In the end we simply need to update the gateway and the Primary key. The DEVEUI and DevAddr can't be updated.
        /// </summary>
        private static IDictionary<string, DevAddrCacheInfo> MergeOldAndNewChanges(IDictionary<string, DevAddrCacheInfo> valueArrayBase, IDictionary<string, DevAddrCacheInfo> valueArrayimport, bool shouldImportFromNewValues)
        {
            var isSaveRequired = false;
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
                    _ = valueArrayimport.Remove(baseValue.Key);
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
                    valueArrayBase.Add(remainingElementToImport.Value.DevEUI.Value.ToString(), remainingElementToImport.Value);
                }
            }

            // If no changes are required we return null to avoid saving and updating the expiry on the cache.
            return isSaveRequired ? valueArrayBase : null;
        }

        /// <summary>
        /// Method to take a lock when querying IoT Hub for a primary key.
        /// It is blocking as only one should access it.
        /// </summary>
        public async Task<bool> TryTakeDevAddrUpdateLock(DevAddr devAddr)
        {
            return await this.cacheStore.LockTakeAsync(string.Concat(DevAddrLockName, devAddr), this.lockOwner, DefaultSingleLockExpiry, block: true);
        }

        public bool ReleaseDevAddrUpdateLock(DevAddr devAddr)
        {
            return this.cacheStore.LockRelease(string.Concat(DevAddrLockName, devAddr), this.lockOwner);
        }
    }
}
