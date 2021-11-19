// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
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
        private readonly NetworkServerConfiguration configuration;
        private readonly string devAddr;
        private readonly LoRaDeviceCache loraDeviceCache;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;
        private volatile LoaderState state;
        private readonly object queueLock;
        private volatile List<LoRaRequest> queuedRequests;

        protected virtual bool LoadingDevicesFailed { get; set; }

        internal DeviceLoaderSynchronizer(
            string devAddr,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceFactory deviceFactory,
            NetworkServerConfiguration configuration,
            LoRaDeviceCache deviceCache,
            HashSet<ILoRaDeviceInitializer> initializers)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.configuration = configuration;
            this.devAddr = devAddr;
            this.loraDeviceCache = deviceCache;
            this.initializers = initializers;
            this.state = LoaderState.QueryingDevices;
            this.queueLock = new object();
            this.queuedRequests = new List<LoRaRequest>();
        }

        internal async Task LoadAsync()
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
                    throw new LoRaProcessingException("Error when searching device for payload.", ex);
                }

                SetState(LoaderState.CreatingDeviceInstances);
                await CreateDevicesAsync(searchDeviceResult.Devices);

                // Dispatch queued requests to created devices
                // those without a matching device will receive "failed" notification
                lock (this.queueLock)
                {
                    SetState(LoaderState.DispatchingQueuedItems);

                    DispatchQueuedItems();

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

        protected async Task CreateDevicesAsync(IReadOnlyList<IoTHubDeviceInfo> devices)
        {
            List<Task<LoRaDevice>> initTasks = null;
            List<LoRaDevice> createdDevices = null;
            List<Task<bool>> refreshTasks = null;

            if (devices?.Count > 0)
            {
                createdDevices = new List<LoRaDevice>(devices.Count);
                initTasks = new List<Task<LoRaDevice>>(devices.Count);

                foreach (var foundDevice in devices)
                {
                    // Only create devices that does not exist in the cache
                    if (!this.loraDeviceCache.TryGetByDevEui(foundDevice.DevEUI, out var cachedDevice))
                    {
                        initTasks.Add(this.deviceFactory.CreateAndRegisterAsync(foundDevice, CancellationToken.None));
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(cachedDevice.DevAddr))
                        {
                            // device in cache from a previous join that we didn't complete
                            // (lost race with another gw) - refresh the twins now and keep it
                            // in the cache
                            refreshTasks ??= new List<Task<bool>>();
                            refreshTasks.Add(cachedDevice.InitializeAsync(this.configuration, CancellationToken.None));
                            Logger.Log(foundDevice.DevEUI, "refreshing device to fetch DevAddr", LogLevel.Debug);
                        }
                    }
                }

                try
                {
                    _ = await Task.WhenAll(initTasks);
                    if (refreshTasks != null)
                    {
                        _ = await Task.WhenAll(refreshTasks);
                    }
                }
                catch (LoRaProcessingException ex) when (ex.ErrorCode == LoRaProcessingErrorCode.DeviceInitializationFailed)
                {
                    Logger.Log(this.devAddr, $"one or more device initializations failed: {ex}", LogLevel.Error);
                    HasLoadingDeviceError = true;
                }
            }

            if (initTasks?.Count > 0)
            {
                foreach (var deviceTask in initTasks)
                {
                    if (deviceTask.IsCompletedSuccessfully)
                    {
                        var device = deviceTask.Result;
                        // run initializers
                        InitializeDevice(device);
                        createdDevices.Add(device);
                    }
                }
            }

            CreatedDevicesCount = createdDevices != null ? createdDevices.Count : 0;
        }

        private void NotifyQueueItemsDueToError(LoRaDeviceRequestFailedReason loRaDeviceRequestFailedReason = LoRaDeviceRequestFailedReason.ApplicationError)
        {
            List<LoRaRequest> failedRequests;
            lock (this.queueLock)
            {
                failedRequests = this.queuedRequests;
                this.queuedRequests = new List<LoRaRequest>();
                LoadingDevicesFailed = true;
            }

            failedRequests.ForEach(x => x.NotifyFailed(loRaDeviceRequestFailedReason));
        }

        private void DispatchQueuedItems()
        {
            foreach (var request in this.queuedRequests)
            {
                ProcessRequest(request);
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
                ProcessRequest(request);
            }
        }

        protected virtual void ProcessRequest(LoRaRequest request)
        {
            if (LoadingDevicesFailed)
            {
                LogAndNotifyFailedRequest(LoRaDeviceRequestFailedReason.ApplicationError);
                return;
            }

            var devAddr = ConversionHelper.ByteArrayToString(request.Payload.DevAddr);
            if (!this.loraDeviceCache.HasRegistrations(devAddr))
            {
                LogAndNotifyFailedRequest(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr);
                return;
            }

            if (this.loraDeviceCache.TryGetForPayload(request.Payload, out var device))
            {
                if (device.IsOurDevice)
                {
                    device.Queue(request);
                }
                else
                {
                    LogAndNotifyFailedRequest(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);
                }
            }
            else if (this.loraDeviceCache.HasRegistrationsForOtherGateways(devAddr))
            {
                LogAndNotifyFailedRequest(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);
            }
            else
            {
                LogAndNotifyFailedRequest(LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck);
            }

            void LogAndNotifyFailedRequest(LoRaDeviceRequestFailedReason reason)
            {
                LogRequestFailed(request, reason);
                request.NotifyFailed(reason);
            }
        }

        private void SetState(LoaderState newState)
        {
            if (this.state != newState)
            {
                this.state = newState;
            }
        }

        private static void LogRequestFailed(LoRaRequest request, LoRaDeviceRequestFailedReason failedReason)
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

                case LoRaDeviceRequestFailedReason.BelongsToAnotherGateway:
                    Logger.Log(deviceId, "device is not our device, ignore message", LogLevel.Debug);
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
                    Logger.Log(deviceId, "device request failed", LogLevel.Debug);
                    break;
            }
        }

        private void InitializeDevice(LoRaDevice loRaDevice)
        {
            if (loRaDevice.IsOurDevice)
            {
                // once added, call initializers
                if (this.initializers != null)
                {
                    foreach (var initializer in this.initializers)
                        initializer.Initialize(loRaDevice);
                }
            }
        }
    }
}
