// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    internal class EdgeDeviceGetter
    {
        private readonly IDeviceRegistryManager registryManager;
        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly ILogger<EdgeDeviceGetter> logger;

        public EdgeDeviceGetter(IDeviceRegistryManager registryManager,
                                ILoRaDeviceCacheStore cacheStore,
                                ILogger<EdgeDeviceGetter> logger)
        {
            this.registryManager = registryManager;
            this.cacheStore = cacheStore;
            this.logger = logger;
        }

        private async Task<List<Twin>> GetEdgeDevicesAsync()
        {
            this.logger.LogDebug("Getting Azure IoT Edge devices");
            var q = this.registryManager.CreateQuery("SELECT * FROM devices where capabilities.iotEdge = true");
            var twins = new List<Twin>();
            do
            {
                twins.AddRange(await q.GetNextAsTwinAsync());
            } while (q.HasMoreResults);
            return twins;
        }

        internal async Task<bool> IsEdgeDeviceAsync(string lnsId)
        {
            var keyLock = $"{nameof(EdgeDeviceGetter)}-lock";
            var owner = nameof(EdgeDeviceGetter);
            var isEdgeDevice = false;
            try
            {
                if (await cacheStore.LockTakeAsync(keyLock, owner, TimeSpan.FromSeconds(10)))
                {
                    var findInCache = () => this.cacheStore.GetObject<DeviceKind>(lnsId);
                    if (findInCache() is null)
                    {
                        await RefreshEdgeDevicesCacheAsync();
                        isEdgeDevice = findInCache() is { IsEdge: true };
                        if (!isEdgeDevice)
                            _ = MarkDeviceAsNonEdge(lnsId);
                    }
                }
                else
                {
                    throw new TimeoutException("Timed out while taking a lock on Redis Edge Device cache");
                }
            }
            finally
            {
                _ = cacheStore.LockRelease(keyLock, owner);
            }
            return isEdgeDevice;
        }

        private bool MarkDeviceAsNonEdge(string lnsId)
            => this.cacheStore.ObjectSet(lnsId,
                                         new DeviceKind(isEdge: false),
                                         TimeSpan.FromDays(1),
                                         onlyIfNotExists: true);

        private async Task RefreshEdgeDevicesCacheAsync()
        {
            this.logger.LogDebug("Refreshing Azure IoT Edge devices cache");
            var twins = await GetEdgeDevicesAsync();
            foreach (var t in twins)
            {
                _ = this.cacheStore.ObjectSet(t.DeviceId,
                                              new DeviceKind(isEdge: true),
                                              TimeSpan.FromDays(1),
                                              onlyIfNotExists: true);
            }
        }
    }

    internal class DeviceKind
    {
        public bool IsEdge { get; private set; }
        public DeviceKind(bool isEdge)
        {
            IsEdge = isEdge;
        }
    }
}
