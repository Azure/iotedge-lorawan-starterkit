// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DeduplicationExecutionItem : IFunctionBundlerExecutionItem
    {
        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly IServiceClient serviceClient;

        public DeduplicationExecutionItem(
            ILoRaDeviceCacheStore cacheStore,
            IServiceClient serviceClient)
        {
            this.cacheStore = cacheStore;
            this.serviceClient = serviceClient;
        }

        public async Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            if (context is null) throw new System.ArgumentNullException(nameof(context));

            context.Result.DeduplicationResult = await GetDuplicateMessageResultAsync(context.DevEUI, context.Request.GatewayId, context.Request.ClientFCntUp, context.Request.ClientFCntDown, context.Logger);

            return context.Result.DeduplicationResult.IsDuplicate ? FunctionBundlerExecutionState.Abort : FunctionBundlerExecutionState.Continue;
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

        internal async Task<DuplicateMsgResult> GetDuplicateMessageResultAsync(DevEui devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown, ILogger logger = null)
        {
            var isDuplicate = true;
            var processedDevice = gatewayId;

            using (var deviceCache = new LoRaDeviceCache(this.cacheStore, devEUI, gatewayId))
            {
                if (await deviceCache.TryToLockAsync())
                {
                    // we are owning the lock now
                    if (deviceCache.TryGetInfo(out var cachedDeviceState))
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
                            var previousGateway = cachedDeviceState.GatewayId;

                            cachedDeviceState.FCntUp = clientFCntUp;
                            cachedDeviceState.GatewayId = gatewayId;
                            _ = deviceCache.StoreInfo(cachedDeviceState);

                            if (previousGateway != gatewayId)
                            {
                                var loraC2DMessage = new LoRaCloudToDeviceMessage()
                                {
                                    DevEUI = devEUI,
                                    Fport = FramePort.DropConnectionCommand
                                };
                                using var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(loraC2DMessage)));
                                try
                                {
                                    logger?.LogDebug($"Sending C2D message to LNS: {previousGateway} to drop connection for device: {devEUI}");
                                    await this.serviceClient.SendAsync(previousGateway, message);
                                }
                                catch (IotHubException ex)
                                {
                                    logger?.LogError(ex, $"Failed to send C2D message to LNS: {previousGateway} to drop the connection for device: {devEUI}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // initialize
                        isDuplicate = false;
                        var state = deviceCache.Initialize(clientFCntUp, clientFCntDown);
                        logger?.LogDebug("initialized state for {id}:{gwid} = {state}", devEUI, gatewayId, state);
                    }
                }
                else
                {
                    processedDevice = "[unknown]";
                    logger?.LogWarning("Failed to acquire lock");
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
