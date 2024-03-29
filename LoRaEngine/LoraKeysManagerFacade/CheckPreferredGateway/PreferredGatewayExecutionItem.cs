// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Checks the preferred gateway for a device.
    /// </summary>
    /// <remarks>
    /// The resolution happens the following way:
    /// - Request for the device and fcntUp are stored in a list
    /// - Wait 200ms for other gateways register the same request for the device
    /// - Read all requests and identity the closest gateway based on RSSI
    /// - A single azure function will update the computed value
    /// - All pending calls will return the computed value.
    /// </remarks>
    public class PreferredGatewayExecutionItem : IFunctionBundlerExecutionItem
    {
        public const int MaxAttemptsToResolvePreferredGateway = 10;
        public const int RequestListCacheDurationInMinutes = 3;
        public const int DefaultReceiveRequestsPeriodInMs = 200;
        public const string PreferredGatewayReceiveRequestsConfigurationname = "PREFERRED_GATEWAY_REQUESTS_INTERVAL";

        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly ILogger<PreferredGatewayExecutionItem> log;
        private readonly int receiveInterval;

        public PreferredGatewayExecutionItem(ILoRaDeviceCacheStore cacheStore, ILogger<PreferredGatewayExecutionItem> log, IConfiguration configuration)
        {
            this.log = log;
            this.cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));

            var receiveIntervalSetting = configuration?.GetValue<string>(PreferredGatewayReceiveRequestsConfigurationname);
            if (!string.IsNullOrEmpty(receiveIntervalSetting) && int.TryParse(receiveIntervalSetting, out var receiveIntervalSettingInt) && receiveIntervalSettingInt > 0)
            {
                this.receiveInterval = receiveIntervalSettingInt;
            }
            else
            {
                this.receiveInterval = DefaultReceiveRequestsPeriodInMs;
            }

            this.log.LogInformation("Using receive interval for in preferred gateway of {interval}ms", this.receiveInterval);
        }

        public int Priority => 4;

        public async Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            await ComputeAndSetPreferredGateway(context);

            return FunctionBundlerExecutionState.Continue;
        }

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.PreferredGateway) == FunctionBundlerItemType.PreferredGateway;
        }

        public async Task OnAbortExecutionAsync(IPipelineExecutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            await ComputeAndSetPreferredGateway(context);
        }

        private async Task ComputeAndSetPreferredGateway(IPipelineExecutionContext context)
        {
            context.Result.PreferredGatewayResult = await ComputePreferredGateway(context);
        }

        private async Task<PreferredGatewayResult> ComputePreferredGateway(IPipelineExecutionContext context)
        {
            var computationId = Guid.NewGuid().ToString();
            var fcntUp = context.Request.ClientFCntUp;
            var devEUI = context.DevEUI;
            var rssi = context.Request.Rssi.Value;

            // 1. Add request to list `preferred_gateway:deviceID:fcnt`, timeout: 5min
            //    List item: gatewayid, rssi, insertTime
            var item = new PreferredGatewayTableItem(context.Request.GatewayId, rssi);
            var listCacheKey = LoRaDevicePreferredGateway.PreferredGatewayFcntUpItemListCacheKey(devEUI, fcntUp);
            _ = this.cacheStore.ListAdd(listCacheKey, item.ToCachedString(), TimeSpan.FromMinutes(RequestListCacheDurationInMinutes));
            this.log.LogInformation("Preferred gateway {devEUI}/{fcnt}: added {gateway} with {rssi}", devEUI, fcntUp, context.Request.GatewayId, rssi);

            // 2. Wait for the time specified in receiveInterval (default 200ms). Optional: wait less if another requests already started
            await Task.Delay(this.receiveInterval);

            // 3. Check if value was already calculated
            var preferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, devEUI);
            if (preferredGateway != null)
            {
                if (preferredGateway.FcntUp >= fcntUp)
                {
                    return new PreferredGatewayResult(fcntUp, preferredGateway);
                }
            }

            // 4. To calculate, we need to acquire a lock
            var preferredGatewayLockKey = $"preferredGateway:{devEUI}:lock";

            for (var i = 0; i < MaxAttemptsToResolvePreferredGateway; i++)
            {
                if (await this.cacheStore.LockTakeAsync(preferredGatewayLockKey, computationId, TimeSpan.FromMilliseconds(200), block: false))
                {
                    try
                    {
                        preferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, devEUI);
                        if (preferredGateway == null || preferredGateway.FcntUp < fcntUp)
                        {
                            var items = this.cacheStore.ListGet(listCacheKey).Select(x => PreferredGatewayTableItem.CreateFromCachedString(x));

                            // if no table item was found (redis restarted, or delayed processing)?
                            // Return error, we don't want to save a value for each gateway or overwrite with a delayed request
                            var winner = items?.OrderByDescending(x => x.Rssi).FirstOrDefault();
                            if (winner == null)
                            {
                                this.log.LogError("Could not resolve closest gateway in {devEUI} and {fcntUp}", devEUI, fcntUp);

                                return new PreferredGatewayResult(fcntUp, "Could not resolve closest gateway");
                            }

                            preferredGateway = new LoRaDevicePreferredGateway(winner.GatewayID, fcntUp);
                            if (!LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, context.DevEUI, preferredGateway))
                            {
                                this.log.LogWarning("Failed to store preferred gateway for {devEUI}. Gateway: {gateway}", devEUI, context.Request.GatewayId);
                            }

                            this.log.LogInformation("Resolved preferred gateway {devEUI}/{fcnt}: {gateway} with {rssi}", devEUI, fcntUp, context.Request.GatewayId, rssi);
                        }
                    }
                    finally
                    {
                        _ = this.cacheStore.LockRelease(preferredGatewayLockKey, computationId);
                    }
                }
                else
                {
                    // We couldn't get lock
                    // wait a bit and try to get result
                    await Task.Delay(Math.Max(50, this.receiveInterval / 4));
                    preferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, context.DevEUI);
                }

                if (preferredGateway != null)
                {
                    if (preferredGateway.FcntUp >= fcntUp)
                    {
                        return new PreferredGatewayResult(fcntUp, preferredGateway);
                    }
                }
            }

            this.log.LogError("Could not resolve closest gateway in {devEUI} and {fcntUp}", devEUI, fcntUp);
            return new PreferredGatewayResult(fcntUp, "Could not resolve closest gateway");
        }
    }
}
