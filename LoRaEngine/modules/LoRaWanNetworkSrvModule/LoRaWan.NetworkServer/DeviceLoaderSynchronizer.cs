// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
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
        private readonly DevAddr devAddr;
        private readonly LoRaDeviceCache loraDeviceCache;
        private readonly HashSet<ILoRaDeviceInitializer> initializers;
        private readonly ILogger<DeviceLoaderSynchronizer> logger;
        private volatile LoaderState state;
        private readonly object queueLock;
        private volatile List<LoRaRequest> queuedRequests;

        protected virtual bool LoadingDevicesFailed { get; set; }

        internal DeviceLoaderSynchronizer(
            DevAddr devAddr,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceFactory deviceFactory,
            NetworkServerConfiguration configuration,
            LoRaDeviceCache deviceCache,
            HashSet<ILoRaDeviceInitializer> initializers,
            ILogger<DeviceLoaderSynchronizer> logger)
        {
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.deviceFactory = deviceFactory;
            this.configuration = configuration;
            this.devAddr = devAddr;
            this.loraDeviceCache = deviceCache;
            this.initializers = initializers;
            this.logger = logger;
            this.state = LoaderState.QueryingDevices;
            this.queueLock = new object();
            this.queuedRequests = new List<LoRaRequest>();
        }

        internal async Task LoadAsync()
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
            catch (Exception ex)
            {
                NotifyQueueItemsDueToError(LoRaDeviceRequestFailedReason.ApplicationError);
                this.logger.LogError(ex, "Failed to load devices");
                throw;
            }
            finally
            {
                SetState(LoaderState.Finished);
            }
        }

        protected async Task CreateDevicesAsync(IReadOnlyList<IoTHubDeviceInfo> devices)
        {
            if (devices?.Count > 0)
            {
                var deviceCreated = 0;
                var initTasks = new List<Task<LoRaDevice>>(devices.Count);
                List<Task> refreshTasks = null;
                List<Exception> deviceInitExceptionList = null;

                foreach (var foundDevice in devices)
                {
                    using var scope = this.logger.BeginDeviceScope(foundDevice.DevEUI);
                    // Only create devices that does not exist in the cache
                    if (!this.loraDeviceCache.TryGetByDevEui(foundDevice.DevEUI, out var cachedDevice))
                    {
                        initTasks.Add(this.deviceFactory.CreateAndRegisterAsync(foundDevice, CancellationToken.None));
                    }
                    else
                    {
                        if (cachedDevice.DevAddr is null)
                        {
                            // device in cache from a previous join that we didn't complete
                            // (lost race with another gw) - refresh the twins now and keep it
                            // in the cache
                            refreshTasks ??= new List<Task>();
                            refreshTasks.Add(RefreshDeviceAsync(cachedDevice));
                            this.logger.LogDebug("refreshing device to fetch DevAddr");
                        }
                        else
                        {
                            // this case covers a cached device with a potentially outdated DevAddr.
                            // we want to disconnect it.
                            // if the device rejoined, a new DevAddr should have been
                            // generated, therefore we don't need this stale connection anymore.
                            // If instead it is all up to date, the connection will be re-established
                            // as soon as the data message is processed.
                            if (cachedDevice.IsConnectionOwner is true)
                            {
                                this.logger.LogDebug("stale connection owner, releasing the connection.");
                                cachedDevice.IsConnectionOwner = false;
                            }
                            await cachedDevice.CloseConnectionAsync(CancellationToken.None);
                        }
                    }
                }

                async Task RefreshDeviceAsync(LoRaDevice device)
                {
                    try
                    {
                        _ = await device.InitializeAsync(this.configuration, CancellationToken.None);
                    }
                    finally
                    {
                        await device.CloseConnectionAsync(CancellationToken.None);
                    }
                }

                try
                {
                    _ = await Task.WhenAll(initTasks);
                    if (refreshTasks != null)
                    {
                        await Task.WhenAll(refreshTasks);
                    }
                }
                catch (LoRaProcessingException ex) when (ex.ErrorCode == LoRaProcessingErrorCode.DeviceInitializationFailed
                                                         && ExceptionFilterUtility.True(() => this.logger.LogError($"one or more device initializations failed: {ex}")))
                {
                    // continue
                    HasLoadingDeviceError = true;
                }

                if (initTasks.Count > 0)
                {
                    foreach (var deviceTask in initTasks)
                    {
                        if (deviceTask.IsCompletedSuccessfully)
                        {
                            var device = await deviceTask;
                            // run initializers
                            try
                            {
                                InitializeDevice(device);
                                deviceCreated++;
                            }
#pragma warning disable CA1031 // Do not catch general exception types (captured and thrown later)
                            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                            {
#pragma warning disable CA1508 // Avoid dead conditional code (false positive)
                                deviceInitExceptionList ??= new List<Exception>();
#pragma warning restore CA1508 // Avoid dead conditional code
                                deviceInitExceptionList.Add(ex);
                            }
                            finally
                            {
                                await device.CloseConnectionAsync(CancellationToken.None);
                            }
                        }
                    }
                }

                CreatedDevicesCount = deviceCreated;

                if (deviceInitExceptionList is { Count: > 0 } someExceptions)
                    throw new AggregateException(someExceptions);
            }
            else
            {
                CreatedDevicesCount = 0;
            }
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

            if (!this.loraDeviceCache.HasRegistrations(this.devAddr))
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
            else if (this.loraDeviceCache.HasRegistrationsForOtherGateways(this.devAddr))
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

        private void LogRequestFailed(LoRaRequest request, LoRaDeviceRequestFailedReason failedReason)
        {
            switch (failedReason)
            {
                case LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck:
                    this.logger.LogDebug($"with devAddr {request.Payload.DevAddr} check MIC failed");
                    break;

                case LoRaDeviceRequestFailedReason.BelongsToAnotherGateway:
                case LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr:
                    this.logger.LogDebug("device is not our device, ignore message");
                    break;

                case LoRaDeviceRequestFailedReason.ApplicationError:
                    this.logger.LogError("problem resolving device");
                    break;

                case LoRaDeviceRequestFailedReason.InvalidNetId:
                case LoRaDeviceRequestFailedReason.InvalidUpstreamMessage:
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

        private void InitializeDevice(LoRaDevice loRaDevice)
        {
            if (loRaDevice.IsOurDevice && this.initializers != null)
            {
                foreach (var initializer in this.initializers)
                    initializer.Initialize(loRaDevice);
            }
        }
    }
}
