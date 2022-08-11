// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.Extensions.Logging;

    public class EdgeDeviceGetter : IEdgeDeviceGetter
    {
        private readonly IDeviceRegistryManager registryManager;
        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly ILogger<EdgeDeviceGetter> logger;
        private DateTimeOffset? lastUpdateTime;

        public EdgeDeviceGetter(IDeviceRegistryManager registryManager,
                                ILoRaDeviceCacheStore cacheStore,
                                ILogger<EdgeDeviceGetter> logger)
        {
            this.registryManager = registryManager;
            this.cacheStore = cacheStore;
            this.logger = logger;
        }

        private async Task<IEnumerable<IDeviceTwin>> GetEdgeDevicesAsync(CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Getting Azure IoT Edge devices");
            var twins = new List<IDeviceTwin>();
            var query = this.registryManager.GetEdgeDevices();

            do
            {
                var items = await query.GetNextPageAsync();

                twins.AddRange(items);
            } while (query.HasMoreResults && !cancellationToken.IsCancellationRequested);

            return twins;
        }

        public async Task<bool> IsEdgeDeviceAsync(string lnsId, CancellationToken cancellationToken)
        {
            const string keyLock = $"{nameof(EdgeDeviceGetter)}-lock";
            const string owner = nameof(EdgeDeviceGetter);
            var isEdgeDevice = false;
            try
            {
                if (await this.cacheStore.LockTakeAsync(keyLock, owner, TimeSpan.FromSeconds(10)))
                {
                    var findInCache = () => this.cacheStore.GetObject<DeviceKind>(RedisLnsDeviceCacheKey(lnsId));
                    var firstSearch = findInCache();
                    if (firstSearch is null)
                    {
                        await RefreshEdgeDevicesCacheAsync(cancellationToken);
                        isEdgeDevice = findInCache() is { IsEdge: true };
                        if (!isEdgeDevice)
                        {
                            var marked = MarkDeviceAsNonEdge(lnsId);
                            if (!marked)
                                this.logger.LogError("Could not update Redis Edge Device cache status for device {}", lnsId);
                        }
                    }
                    else
                    {
                        return firstSearch.IsEdge;
                    }
                }
                else
                {
                    throw new TimeoutException("Timed out while taking a lock on Redis Edge Device cache");
                }
            }
            finally
            {
                _ = this.cacheStore.LockRelease(keyLock, owner);
            }
            return isEdgeDevice;
        }

        private static string RedisLnsDeviceCacheKey(string lnsId) => $"lnsInstance-{lnsId}";

        private bool MarkDeviceAsNonEdge(string lnsId)
            => this.cacheStore.ObjectSet(RedisLnsDeviceCacheKey(lnsId),
                                         new DeviceKind(isEdge: false),
                                         TimeSpan.FromDays(1),
                                         onlyIfNotExists: true);

        private async Task RefreshEdgeDevicesCacheAsync(CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Refreshing Azure IoT Edge devices cache");
            if (this.lastUpdateTime is null
                || this.lastUpdateTime - DateTimeOffset.UtcNow >= TimeSpan.FromMinutes(1))
            {
                var twins = await GetEdgeDevicesAsync(cancellationToken);
                foreach (var t in twins)
                {
                    _ = this.cacheStore.ObjectSet(RedisLnsDeviceCacheKey(t.DeviceId),
                                                  new DeviceKind(isEdge: true),
                                                  TimeSpan.FromDays(1),
                                                  onlyIfNotExists: true);
                }
                this.lastUpdateTime = DateTimeOffset.UtcNow;
            }
        }

        public async Task<ICollection<string>> ListEdgeDevicesAsync(CancellationToken cancellationToken)
        {
            var edgeDevices = await GetEdgeDevicesAsync(cancellationToken);
            return edgeDevices.Select(e => e.DeviceId).ToList();
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
