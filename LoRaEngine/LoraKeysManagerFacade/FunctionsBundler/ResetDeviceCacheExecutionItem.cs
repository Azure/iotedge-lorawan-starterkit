// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaTools.CommonAPI;
    using Microsoft.Extensions.Logging;

    public class ResetDeviceCacheExecutionItem : IFunctionBundlerExecutionItem
    {
        private readonly ILoRaDeviceCacheStore deviceCacheStore;

        public ResetDeviceCacheExecutionItem(ILoRaDeviceCacheStore deviceCacheStore)
        {
            this.deviceCacheStore = deviceCacheStore;
        }

        public Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            using (var deviceCache = new LoRaDeviceCache(this.deviceCacheStore, context.DevEUI, context.Request.GatewayId))
            {
                if (deviceCache.TryToLock())
                {
                    deviceCache.ClearCache();
                }
                else
                {
                    context.Logger.LogWarning("Failed to get lock for cache reset");
                }
            }

            return Task.FromResult(FunctionBundlerExecutionState.Continue);
        }

        public int Priority => 0;

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.ResetDeviceCache) == FunctionBundlerItemType.ResetDeviceCache;
        }

        public Task OnAbortExecutionAsync(IPipelineExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
