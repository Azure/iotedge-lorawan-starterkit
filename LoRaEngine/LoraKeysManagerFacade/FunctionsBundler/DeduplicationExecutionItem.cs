// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.CommonAPI;
    using LoRaWan;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Metrics;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DeduplicationExecutionItem : IFunctionBundlerExecutionItem
    {
        private const string ConnectionOwnershipChangeMetricName = "ConnectionOwnershipChange";

        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly IServiceClient serviceClient;
        private readonly IEdgeDeviceGetter edgeDeviceGetter;
        private readonly IChannelPublisher channelPublisher;
        private readonly Microsoft.ApplicationInsights.Metric connectionOwnershipChangedMetric;

        private static readonly TimeSpan DuplicateMessageTimeout = TimeSpan.FromSeconds(30);

        public DeduplicationExecutionItem(
            ILoRaDeviceCacheStore cacheStore,
            IServiceClient serviceClient,
            IEdgeDeviceGetter edgeDeviceGetter,
            IChannelPublisher channelPublisher,
            TelemetryConfiguration telemetryConfiguration)
        {
            this.cacheStore = cacheStore;
            this.serviceClient = serviceClient;
            this.edgeDeviceGetter = edgeDeviceGetter;
            this.channelPublisher = channelPublisher;
            var telemetryClient = new TelemetryClient(telemetryConfiguration);
            var metricIdentifier = new MetricIdentifier(LoraKeysManagerFacadeConstants.MetricNamespace, ConnectionOwnershipChangeMetricName);
            this.connectionOwnershipChangedMetric = telemetryClient.GetMetric(metricIdentifier);
        }

        public async Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

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
            using var cts = new CancellationTokenSource(DuplicateMessageTimeout);
            var isDuplicate = true;
            var processedDevice = gatewayId;

            using (var deviceCache = new LoRaDeviceCache(this.cacheStore, devEUI, gatewayId))
            {
                if (await deviceCache.TryToLockAsync())
                {
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
                                this.connectionOwnershipChangedMetric.TrackValue(1);

                                var loraC2DMessage = new LoRaCloudToDeviceMessage()
                                {
                                    DevEUI = devEUI,
                                    Fport = FramePort.AppMin,
                                    MessageId = Guid.NewGuid().ToString()
                                };

                                var method = new CloudToDeviceMethod(LoraKeysManagerFacadeConstants.CloudToDeviceCloseConnection, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                                var jsonContents = JsonConvert.SerializeObject(loraC2DMessage);
                                _ = method.SetPayloadJson(jsonContents);

                                try
                                {
                                    if (await this.edgeDeviceGetter.IsEdgeDeviceAsync(previousGateway, cts.Token))
                                    {
                                        var res = await this.serviceClient.InvokeDeviceMethodAsync(previousGateway, Constants.NetworkServerModuleId, method, default);
                                        logger?.LogDebug("Connection owner changed and direct method was called on previous gateway '{PreviousConnectionOwner}' to close connection; result is '{Status}'", previousGateway, res?.Status);

                                        if (res is null || (res is { } && !HttpUtilities.IsSuccessStatusCode(res.Status)))
                                        {
                                            logger?.LogError("Failed to invoke direct method on LNS '{PreviousConnectionOwner}' to close the connection for device '{DevEUI}'; status '{Status}'", previousGateway, devEUI, res?.Status);
                                        }

                                    }
                                    else
                                    {
                                        await this.channelPublisher.PublishAsync(previousGateway, new LnsRemoteCall(RemoteCallKind.CloseConnection, jsonContents));
                                        logger?.LogDebug("Connection owner changed and message was published to previous gateway '{PreviousConnectionOwner}' to close connection", previousGateway);
                                    }
                                }
                                catch (IotHubException ex)
                                {
                                    logger?.LogError(ex, "Exception when invoking direct method on LNS '{PreviousConnectionOwner}' to close the connection for device '{DevEUI}'", previousGateway, devEUI);

                                    // The exception is not rethrown because closing the connection on the losing gateway
                                    // is performed on best effort basis.
                                }
                            }
                        }
                    }
                    else
                    {
                        // initialize
                        isDuplicate = false;
                        var state = deviceCache.Initialize(clientFCntUp, clientFCntDown);
                        logger?.LogDebug("Connection owner for {DevEui} set to {GatewayId}; state {State}", devEUI, gatewayId, state);
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
