// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    public class DeduplicationExecutionItem : IFunctionBundlerExecutionItem
    {
        private readonly ILoRaDeviceCacheStore cacheStore;

        public DeduplicationExecutionItem(ILoRaDeviceCacheStore cacheStore)
        {
            this.cacheStore = cacheStore;
        }

        public Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            context.Result.DeduplicationResult = this.GetDuplicateMessageResult(context.DevEUI, context.Request.GatewayId, context.Request.ClientFCntUp, context.Request.ClientFCntDown);
            return Task.FromResult(context.Result.DeduplicationResult.IsDuplicate ? FunctionBundlerExecutionState.Abort : FunctionBundlerExecutionState.Continue);
        }

        public int Priority => 1;

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.Deduplication) == FunctionBundlerItemType.Deduplication;
        }

        public Task OnAbortExecutionAsync(IPipelineExecutionContext context)
        {
            return Task.CompletedTask;
        }

        internal DuplicateMsgResult GetDuplicateMessageResult(string devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            var isDuplicate = true;
            string processedDevice = gatewayId;

            using (var deviceCache = new LoRaDeviceCache(this.cacheStore, devEUI, gatewayId))
            {
                if (deviceCache.TryToLock())
                {
                    // we are owning the lock now
                    if (deviceCache.TryGetInfo(out DeviceCacheInfo cachedDeviceState))
                    {
                        var updateCacheState = false;

                        if (cachedDeviceState.FCntUp < clientFCntUp)
                        {
                            isDuplicate = false;
                            updateCacheState = true;
                        }
                        else if (cachedDeviceState.FCntUp == clientFCntUp && cachedDeviceState.GatewayId == gatewayId)
                        {
                            isDuplicate = false;
                            processedDevice = cachedDeviceState.GatewayId;
                        }
                        else
                        {
                            processedDevice = cachedDeviceState.GatewayId;
                        }

                        if (updateCacheState)
                        {
                            cachedDeviceState.FCntUp = clientFCntUp;
                            cachedDeviceState.GatewayId = gatewayId;
                            deviceCache.StoreInfo(cachedDeviceState);
                        }
                    }
                    else
                    {
                        // initialize
                        isDuplicate = false;
                        deviceCache.Initialize(clientFCntUp, clientFCntDown);
                    }
                }
            }

            return new DuplicateMsgResult
            {
                IsDuplicate = isDuplicate,
                GatewayId = processedDevice
            };
        }
    }
}
