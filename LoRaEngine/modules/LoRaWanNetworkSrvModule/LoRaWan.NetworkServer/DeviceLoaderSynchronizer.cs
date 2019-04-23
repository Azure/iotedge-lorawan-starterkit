// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents a running task loading devices by devAddr
    /// - Prevents querying the registry and loading twins multiple times
    /// - Ensure that requests are queued by <see cref="LoRaDevice"/> in the order they come
    /// </summary>
    class DeviceLoaderSynchronizer : ILoRaDeviceRequestQueue
    {
        internal enum LoaderState
        {
            QueryingDevices,

            CreatingDeviceInstances,

            DispatchingQueuedItems,

            Finished
        }

        /// <summary>
        /// Gets a value indicating whether gets if there were errors loading devices
        /// </summary>
        internal bool HasLoadingDeviceError { get; private set; }

        /// <summary>
        /// Gets the amount of devices that were loaded
        /// </summary>
        internal int CreatedDevicesCount { get; private set; }

        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceFactory deviceFactory;
        private readonly string devAddr;
        private readonly DevEUIToLoRaDeviceDictionary existingDevices;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;
        private readonly NetworkServerConfiguration configuration;
        private readonly Action<LoRaDevice> registerDeviceAction;
        private readonly Task loading;
        private volatile LoaderState state;
        private volatile bool loadingDevicesFailed;
        private object queueLock;
        private volatile List<LoRaRequest> queuedRequests;

        internal DeviceLoaderSynchronizer(
            string devAddr,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceFactory deviceFactory,
            DevEUIToLoRaDeviceDictionary destinationDictionary,
            HashSet<ILoRaDeviceInitializer> initializers,
            NetworkServerConfiguration configuration,
            Action<Task, DeviceLoaderSynchronizer> continuationAction,
            Action<LoRaDevice> registerDeviceAction)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.devAddr = devAddr;
            this.existingDevices = destinationDictionary;
            this.initializers = initializers;
            this.configuration = configuration;
            this.registerDeviceAction = registerDeviceAction;
            this.state = LoaderState.QueryingDevices;
            this.loadingDevicesFailed = false;
            this.queueLock = new object();
            this.queuedRequests = new List<LoRaRequest>();
            this.loading = Task.Run(() => this.Load().ContinueWith((t) => continuationAction(t, this), TaskContinuationOptions.ExecuteSynchronously));
        }

        async Task Load()
        {
            try
            {
                // If device was not found, search in the device API, updating local cache
                Logger.Log(this.devAddr, "querying the registry for device", LogLevel.Debug);

                SearchDevicesResult searchDeviceResult = null;
                try
                {
                    searchDeviceResult = await this.loRaDeviceAPIService.SearchByDevAddrAsync(this.devAddr);

                    // If device was not found, search in the device API, updating local cache
                    Logger.Log(this.devAddr, $"querying the registry for devices by devAddr {this.devAddr} found {searchDeviceResult.Devices?.Count ?? 0} devices", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Logger.Log(this.devAddr, $"error searching device for payload. {ex.Message}", LogLevel.Error);
                    throw;
                }

                this.SetState(LoaderState.CreatingDeviceInstances);
                var createdDevices = await this.CreateDevicesAsync(searchDeviceResult.Devices);

                // Dispatch queued requests to created devices
                // those without a matching device will receive "failed" notification
                lock (this.queueLock)
                {
                    this.SetState(LoaderState.DispatchingQueuedItems);

                    this.DispatchQueuedItems(createdDevices);

                    foreach (var device in createdDevices)
                    {
                        this.registerDeviceAction(device);
                    }

                    this.CreatedDevicesCount = createdDevices.Count;

                    this.SetState(LoaderState.Finished);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(this.devAddr, $"failed to create one or more devices. {ex.Message}", LogLevel.Error);
                this.NotifyQueueItemsDueToError();
                throw;
            }
            finally
            {
                this.SetState(LoaderState.Finished);
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
                        var loRaDevice = this.deviceFactory.Create(foundDevice);
                        initTasks.Add(this.InitializeDeviceAsync(loRaDevice));
                    }
                }

                try
                {
                    await Task.WhenAll(initTasks);
                }
                catch (Exception ex)
                {
                    Logger.Log(this.devAddr, $"one or more device initialization failed. {ex.Message}", LogLevel.Error);
                }
            }

            var createdDevices = new List<LoRaDevice>();
            if (initTasks.Count > 0)
            {
                foreach (var deviceTask in initTasks)
                {
                    if (deviceTask.IsCompletedSuccessfully)
                    {
                        var device = deviceTask.Result;

                        if (device != null)
                        {
                            createdDevices.Add(device);
                        }
                        else
                        {
                            // if device twin load fails, error will be logged and device will be null
                            this.HasLoadingDeviceError = true;
                        }
                    }
                    else
                    {
                        this.HasLoadingDeviceError = true;
                    }
                }
            }

            return createdDevices;
        }

        private void NotifyQueueItemsDueToError()
        {
            List<LoRaRequest> failedRequests;
            lock (this.queueLock)
            {
                failedRequests = this.queuedRequests;
                this.queuedRequests = new List<LoRaRequest>();
                this.loadingDevicesFailed = true;
            }

            failedRequests.ForEach(x => x.NotifyFailed(LoRaDeviceRequestFailedReason.ApplicationError));
        }

        private void DispatchQueuedItems(List<LoRaDevice> devices)
        {
            var hasDevicesMatchingDevAddr = (devices.Count + this.existingDevices.Count) > 0;

            foreach (var request in this.queuedRequests)
            {
                var requestHandled = false;
                if (devices.Count > 0)
                {
                    foreach (var device in devices)
                    {
                        if (device.ValidateMic(request.Payload))
                        {
                            this.AddToDeviceQueue(device, request);
                            requestHandled = true;
                            break;
                        }
                    }
                }

                if (!requestHandled)
                {
                    var failedReason = hasDevicesMatchingDevAddr ? LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck : LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr;
                    this.LogRequestFailed(request, failedReason);
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
                foreach (var device in this.existingDevices.Values)
                {
                    if (device.ValidateMic(request.Payload))
                    {
                        this.AddToDeviceQueue(device, request);
                        return;
                    }
                }

                // not handled, raised failed event
                var failedReason =
                    this.loadingDevicesFailed ? LoRaDeviceRequestFailedReason.ApplicationError :
                    this.existingDevices.Count > 0 ? LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck : LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr;

                this.LogRequestFailed(request, failedReason);

                request.NotifyFailed(failedReason);
            }
        }

        private void AddToDeviceQueue(LoRaDevice device, LoRaRequest request)
        {
            if (device.IsOurDevice)
            {
                device.Queue(request);
            }
            else
            {
                Logger.Log(device.DevEUI, $"device is not our device, ignore message", LogLevel.Debug);
                request.NotifyFailed(device, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);
            }
        }

        void SetState(LoaderState newState)
        {
            if (this.state != newState)
            {
                this.state = newState;
            }
        }

        private void LogRequestFailed(LoRaRequest request, LoRaDeviceRequestFailedReason failedReason)
        {
            var deviceId = ConversionHelper.ByteArrayToString(request.Payload.DevAddr);

            switch (failedReason)
            {
                case LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck:
                    Logger.Log(deviceId, $"with devAddr {ConversionHelper.ByteArrayToString(request.Payload.DevAddr)} check MIC failed", LogLevel.Debug);
                    break;

                case LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr:
                    Logger.Log(deviceId, $"device is not our device, ignore message", LogLevel.Debug);
                    break;

                case LoRaDeviceRequestFailedReason.ApplicationError:
                    Logger.Log(deviceId, "problem resolving device", LogLevel.Error);
                    break;
            }
        }

        private async Task<LoRaDevice> InitializeDeviceAsync(LoRaDevice loRaDevice)
        {
            try
            {
                // Calling initialize async here to avoid making async calls in the concurrent dictionary
                // Since only one device will be added, we guarantee that initialization only happens once
                if (await loRaDevice.InitializeAsync())
                {
                    loRaDevice.IsOurDevice = string.IsNullOrEmpty(loRaDevice.GatewayID) || string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase);

                    // once added, call initializers
                    if (this.initializers != null)
                    {
                        foreach (var initializer in this.initializers)
                            initializer.Initialize(loRaDevice);
                    }

                    if (!loRaDevice.IsOurDevice)
                    {
                        // Initialization does not use activity counters
                        // This should not fail
                        if (!loRaDevice.TryDisconnect())
                        {
                            Logger.Log(loRaDevice.DevEUI, "failed to disconnect device from another gateway", LogLevel.Error);
                        }
                    }

                    return loRaDevice;
                }
            }
            catch (Exception ex)
            {
                // device does not have the required properties
                Logger.Log(loRaDevice.DevEUI ?? this.devAddr, $"error initializing device {loRaDevice.DevEUI}. {ex.Message}", LogLevel.Error);
            }

            // instance not used, dispose the connection
            loRaDevice.Dispose();
            return null;
        }
    }
}