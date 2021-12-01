// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents a running task loading devices by devAddr
    /// - Prevents querying the registry and loading twins multiple times
    /// - Ensure that requests are queued by <see cref="LoRaDevice"/> in the order they come.
    /// </summary>
    internal class DeviceLoaderSynchronizer : ILoRaDeviceRequestQueue
    {
        internal enum LoaderState
        {
            QueryingDevices,

            CreatingDeviceInstances,

            DispatchingQueuedItems,

            Finished
        }

        /// <summary>
        /// Gets a value indicating whether gets if there were errors loading devices.
        /// </summary>
        internal bool HasLoadingDeviceError { get; private set; }

        /// <summary>
        /// Gets the amount of devices that were loaded.
        /// </summary>
        internal int CreatedDevicesCount { get; private set; }

        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceFactory deviceFactory;
        private readonly string devAddr;
        private readonly DevEUIToLoRaDeviceDictionary existingDevices;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;
        private readonly NetworkServerConfiguration configuration;
        private readonly Action<LoRaDevice> registerDeviceAction;
        private readonly ILogger<DeviceLoaderSynchronizer> logger;
        private volatile LoaderState state;
        private volatile bool loadingDevicesFailed;
        private readonly object queueLock;
        private volatile List<LoRaRequest> queuedRequests;

        internal DeviceLoaderSynchronizer(
            string devAddr,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceFactory deviceFactory,
            DevEUIToLoRaDeviceDictionary destinationDictionary,
            HashSet<ILoRaDeviceInitializer> initializers,
            NetworkServerConfiguration configuration,
            Action<Task, DeviceLoaderSynchronizer> continuationAction,
            Action<LoRaDevice> registerDeviceAction,
            ILogger<DeviceLoaderSynchronizer> logger,
            Meter meter)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.devAddr = devAddr;
            this.existingDevices = destinationDictionary;
            this.initializers = initializers;
            this.configuration = configuration;
            this.registerDeviceAction = registerDeviceAction;
            this.logger = logger;
            this.state = LoaderState.QueryingDevices;
            this.loadingDevicesFailed = false;
            this.queueLock = new object();
            this.queuedRequests = new List<LoRaRequest>();
            var processingErrorCount = meter?.CreateCounter<int>(MetricRegistry.ProcessingErrors);
            _ = TaskUtil.RunOnThreadPool(async () =>
            {
                using var scope = this.logger.BeginDeviceAddressScope(this.devAddr);

                var t = Load();

                try
                {
                    await t;
                }
                finally
                {
                    continuationAction(t, this);
                }
            },
            ex =>
            {
                this.logger.LogError($"Error while loading: {ex}.");
                processingErrorCount?.Add(1);
            });
        }

        private async Task Load()
        {
            try
            {
                // If device was not found, search in the device API, updating local cache
                this.logger.LogDebug("querying the registry for device");

                SearchDevicesResult searchDeviceResult = null;
                try
                {
                    searchDeviceResult = await this.loRaDeviceAPIService.SearchByDevAddrAsync(this.devAddr);

                    // If device was not found, search in the device API, updating local cache
                    this.logger.LogDebug($"querying the registry for devices by devAddr {this.devAddr} found {searchDeviceResult.Devices?.Count ?? 0} devices");
                }
                catch (Exception ex)
                {
                    throw new LoRaProcessingException("Error when searching device for payload.", ex);
                }

                SetState(LoaderState.CreatingDeviceInstances);
                var createdDevices = await CreateDevicesAsync(searchDeviceResult.Devices);

                // Dispatch queued requests to created devices
                // those without a matching device will receive "failed" notification
                lock (this.queueLock)
                {
                    SetState(LoaderState.DispatchingQueuedItems);

                    DispatchQueuedItems(createdDevices);

                    foreach (var device in createdDevices)
                    {
                        this.registerDeviceAction(device);
                    }

                    CreatedDevicesCount = createdDevices.Count;

                    SetState(LoaderState.Finished);
                }
            }
            catch (Exception)
            {
                NotifyQueueItemsDueToError(LoRaDeviceRequestFailedReason.ApplicationError);
                throw;
            }
            finally
            {
                SetState(LoaderState.Finished);
            }
        }

        private async Task<List<LoRaDevice>> CreateDevicesAsync(IReadOnlyList<IoTHubDeviceInfo> devices)
        {
            var initTasks = new List<Task<LoRaDevice>>();
            if (devices?.Count > 0)
            {
                foreach (var foundDevice in devices)
                {
                    // Only create devices that don't exist in target dictionary
                    if (!this.existingDevices.ContainsKey(foundDevice.DevEUI))
                    {
                        using var scope = this.logger.BeginDeviceScope(foundDevice.DevEUI);
                        var loRaDevice = this.deviceFactory.Create(foundDevice);
                        initTasks.Add(InitializeDeviceAsync(loRaDevice));
                    }
                }

                try
                {
                    _ = await Task.WhenAll(initTasks);
                }
                catch (LoRaProcessingException ex) when (ex.ErrorCode == LoRaProcessingErrorCode.DeviceInitializationFailed
                                                         && ExceptionFilterUtility.True(() => this.logger.LogError($"one or more device initializations failed: {ex}")))
                {
                    // continue
                }
            }

            var createdDevices = new List<LoRaDevice>();
            if (initTasks.Count > 0)
            {
                foreach (var deviceTask in initTasks)
                {
                    if (deviceTask.IsCompletedSuccessfully)
                    {
                        var device = await deviceTask;

                        if (device != null)
                        {
                            createdDevices.Add(device);
                        }
                        else
                        {
                            // if device twin load fails, error will be logged and device will be null
                            HasLoadingDeviceError = true;
                        }
                    }
                    else
                    {
                        HasLoadingDeviceError = true;
                    }
                }
            }

            return createdDevices;
        }

        private void NotifyQueueItemsDueToError(LoRaDeviceRequestFailedReason loRaDeviceRequestFailedReason = LoRaDeviceRequestFailedReason.ApplicationError)
        {
            List<LoRaRequest> failedRequests;
            lock (this.queueLock)
            {
                failedRequests = this.queuedRequests;
                this.queuedRequests = new List<LoRaRequest>();
                this.loadingDevicesFailed = true;
            }

            failedRequests.ForEach(x => x.NotifyFailed(loRaDeviceRequestFailedReason));
        }

        private void DispatchQueuedItems(List<LoRaDevice> devices)
        {
            var hasDevicesMatchingDevAddr = (devices.Count + this.existingDevices.Count) > 0;

            foreach (var request in this.queuedRequests)
            {
                var requestHandled = false;
                var hasDeviceFromAnotherGateway = false;
                if (devices.Count > 0)
                {
                    foreach (var device in devices)
                    {
                        if (device.IsOurDevice)
                        {
                            if (device.ValidateMic(request.Payload))
                            {
                                AddToDeviceQueue(device, request);
                                requestHandled = true;
                                break;
                            }
                        }
                        else
                        {
                            hasDeviceFromAnotherGateway = true;
                        }
                    }
                }

                if (!requestHandled)
                {
                    var failedReason = hasDeviceFromAnotherGateway ? LoRaDeviceRequestFailedReason.BelongsToAnotherGateway :
                        (hasDevicesMatchingDevAddr ? LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck : LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr);
                    LogRequestFailed(request, failedReason);
                    request.NotifyFailed(failedReason);
                }
            }

            this.queuedRequests.Clear();
        }

        public void Queue(LoRaRequest request)
        {
            var requestAddedToQueue = false;
            if (this.state != LoaderState.Finished)
            {
                lock (this.queueLock)
                {
                    // add to the queue only if loading is not yet finished
                    if (this.state < LoaderState.DispatchingQueuedItems)
                    {
                        this.queuedRequests.Add(request);
                        requestAddedToQueue = true;
                    }
                }
            }

            if (!requestAddedToQueue)
            {
                var hasDeviceFromAnotherGateway = false;
                foreach (var device in this.existingDevices.Values)
                {
                    if (device.IsOurDevice)
                    {
                        if (device.ValidateMic(request.Payload))
                        {
                            AddToDeviceQueue(device, request);
                            return;
                        }
                    }
                    else
                    {
                        hasDeviceFromAnotherGateway = true;
                    }
                }

                // not handled, raised failed event
                var failedReason =
                    hasDeviceFromAnotherGateway ? LoRaDeviceRequestFailedReason.BelongsToAnotherGateway :
                    this.loadingDevicesFailed ? LoRaDeviceRequestFailedReason.ApplicationError :
                    this.existingDevices.Count > 0 ? LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck : LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr;

                LogRequestFailed(request, failedReason);

                request.NotifyFailed(failedReason);
            }
        }

        private void AddToDeviceQueue(LoRaDevice device, LoRaRequest request)
        {
            using var scope = this.logger.BeginDeviceScope(device.DevEUI);

            if (device.IsOurDevice)
            {
                device.Queue(request);
            }
            else
            {
                this.logger.LogDebug("device is not our device, ignore message");
                request.NotifyFailed(device, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);
            }
        }

        private void SetState(LoaderState newState)
        {
            if (this.state != newState)
            {
                this.state = newState;
            }
        }

        private void LogRequestFailed(LoRaRequest request, LoRaDeviceRequestFailedReason failedReason)
        {
            switch (failedReason)
            {
                case LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck:
                    this.logger.LogDebug($"with devAddr {ConversionHelper.ByteArrayToString(request.Payload.DevAddr)} check MIC failed");
                    break;

                case LoRaDeviceRequestFailedReason.BelongsToAnotherGateway:
                case LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr:
                    this.logger.LogDebug("device is not our device, ignore message");
                    break;

                case LoRaDeviceRequestFailedReason.ApplicationError:
                    this.logger.LogError("problem resolving device");
                    break;

                case LoRaDeviceRequestFailedReason.InvalidNetId:
                case LoRaDeviceRequestFailedReason.InvalidRxpk:
                case LoRaDeviceRequestFailedReason.InvalidRegion:
                case LoRaDeviceRequestFailedReason.UnknownDevice:
                case LoRaDeviceRequestFailedReason.InvalidJoinRequest:
                case LoRaDeviceRequestFailedReason.HandledByAnotherGateway:
                case LoRaDeviceRequestFailedReason.JoinDevNonceAlreadyUsed:
                case LoRaDeviceRequestFailedReason.JoinMicCheckFailed:
                case LoRaDeviceRequestFailedReason.ReceiveWindowMissed:
                case LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded:
                case LoRaDeviceRequestFailedReason.InvalidFrameCounter:
                case LoRaDeviceRequestFailedReason.IoTHubProblem:
                case LoRaDeviceRequestFailedReason.DeduplicationDrop:
                case LoRaDeviceRequestFailedReason.DeviceClientConnectionFailed:
                default:
                    this.logger.LogDebug("device request failed");
                    break;
            }
        }

        private async Task<LoRaDevice> InitializeDeviceAsync(LoRaDevice loRaDevice)
        {
            try
            {
                // Our device if it does not have a gateway assigned or is assigned to our
                var isOurDevice = string.IsNullOrEmpty(loRaDevice.GatewayID) || string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.OrdinalIgnoreCase);
                // Only create client if the device is our
                if (!isOurDevice)
                {
                    loRaDevice.IsOurDevice = false;
                    return loRaDevice;
                }

                // Calling initialize async here to avoid making async calls in the concurrent dictionary
                // Since only one device will be added, we guarantee that initialization only happens once
                if (await loRaDevice.InitializeAsync())
                {
                    // revalidate based on device twin property
                    loRaDevice.IsOurDevice = string.IsNullOrEmpty(loRaDevice.GatewayID) || string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.OrdinalIgnoreCase);
                    if (loRaDevice.IsOurDevice)
                    {
                        // once added, call initializers
                        if (this.initializers != null)
                        {
                            foreach (var initializer in this.initializers)
                                initializer.Initialize(loRaDevice);
                        }
                    }

                    // checking again in case one of the initializers change the value
                    if (!loRaDevice.IsOurDevice)
                    {
                        // Initialization does not use activity counters
                        // This should not fail
                        if (!loRaDevice.TryDisconnect())
                        {
                            this.logger.LogError("failed to disconnect device from another gateway");
                        }
                    }

                    return loRaDevice;
                }

                // instance not used, dispose the connection
                loRaDevice.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                throw new LoRaProcessingException($"Device initialization of device '{loRaDevice.DevEUI}' failed.", ex, LoRaProcessingErrorCode.DeviceInitializationFailed);
            }
        }
    }
}
