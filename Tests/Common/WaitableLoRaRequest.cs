// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;

    public sealed class WaitableLoRaRequest : LoRaRequest, IDisposable
    {
        private readonly SemaphoreSlim complete = new SemaphoreSlim(0);

        public bool ProcessingFailed { get; private set; }

        public LoRaDeviceRequestFailedReason ProcessingFailedReason { get; private set; }

        public DownlinkMessage ResponseDownlink { get; private set; }

        public bool ProcessingSucceeded { get; private set; }

        private WaitableLoRaRequest(LoRaPayload payload)
            : base(payload)
        { }

        private WaitableLoRaRequest(RadioMetadata radioMetadata, IDownstreamMessageSender downstreamMessageSender, DateTime startTime)
            : base(radioMetadata, downstreamMessageSender, startTime)
        { }

        /// <summary>
        /// Creates a WaitableLoRaRequest using a real time watcher.
        /// </summary>
        public static WaitableLoRaRequest Create(LoRaPayload payload) =>
            new WaitableLoRaRequest(payload);

        /// <summary>
        /// Creates a WaitableLoRaRequest that uses a deterministic time handler.
        /// </summary>
        /// <param name="rxpk">Rxpk instance.</param>
        /// <param name="downstreamMessageSender">DownstreamMessageSender instance.</param>
        /// <param name="startTimeOffset">Is subtracted from the current time to determine the start time for the deterministic time watcher. Default is TimeSpan.Zero.</param>
        /// <param name="constantElapsedTime">Controls how much time is elapsed when querying the time watcher. Default is TimeSpan.Zero.</param>
        /// <param name="useRealTimer">Allows you to opt-in to use a real, non-deterministic time watcher.</param>
        public static WaitableLoRaRequest Create(RadioMetadata radioMetadata,
                                                 LoRaPayload loRaPayload,
                                                 IDownstreamMessageSender downstreamMessageSender = null,
                                                 TimeSpan? startTimeOffset = null,
                                                 TimeSpan? constantElapsedTime = null,
                                                 bool useRealTimer = false,
                                                 Region region = null) =>
            Create(radioMetadata, LoRaEnumerable.RepeatInfinite(constantElapsedTime ?? TimeSpan.Zero), downstreamMessageSender, startTimeOffset, useRealTimer, loRaPayload, region: region);

        /// <summary>
        /// Creates a WwaitableLoRaRequest that is configured to miss certain receive windows.
        /// </summary>
        /// <param name="rxpk">Rxpk instance.</param>
        /// <param name="downstreamMessageSender">DownstreamMessageSender instance.</param>
        /// <param name="inTimeForC2DMessageCheck">If set to true it ensures that processing is fast enough that C2D messages can be checked.</param>
        /// <param name="inTimeForAdditionalMessageCheck">If set to true it ensures that processing is fast enough that additional C2D messages can be checked.</param>
        /// <param name="inTimeForDownlinkDelivery">If set to true it ensures that processing is fast enough that C2D messages can be checked.</param>
        public static WaitableLoRaRequest Create(RadioMetadata radioMetadata,
                                                 IDownstreamMessageSender downstreamMessageSender,
                                                 bool inTimeForC2DMessageCheck,
                                                 bool inTimeForAdditionalMessageCheck,
                                                 bool inTimeForDownlinkDelivery,
                                                 LoRaPayloadData loRaPayloadData = null)
        {
            var c2dMessageCheckTimeSpan = inTimeForC2DMessageCheck ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(10);
            var additionalMessageCheckTimeSpan = inTimeForAdditionalMessageCheck ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(10);
            var downlinkDeliveryTimeSpan = inTimeForDownlinkDelivery ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(10);
            return Create(radioMetadata, new[] { c2dMessageCheckTimeSpan, c2dMessageCheckTimeSpan, additionalMessageCheckTimeSpan, downlinkDeliveryTimeSpan }, downstreamMessageSender: downstreamMessageSender, loRaPayload: loRaPayloadData);
        }
        public static WaitableLoRaRequest CreateWaitableRequest(LoRaPayload loRaPayload,
                                                                RadioMetadata radioMetadata = null,
                                                                IDownstreamMessageSender downstreamMessageSender = null) =>
           Create(radioMetadata ?? TestUtils.GenerateTestRadioMetadata(),
                  loRaPayload,
                  downstreamMessageSender);

        private static WaitableLoRaRequest Create(RadioMetadata radioMetadata,
                                                  IEnumerable<TimeSpan> elapsedTimes,
                                                  IDownstreamMessageSender downstreamMessageSender = null,
                                                  TimeSpan? startTimeOffset = null,
                                                  bool useRealTimer = false,
                                                  LoRaPayload loRaPayload = null,
                                                  Region region = null)
        {
            var request = new WaitableLoRaRequest(radioMetadata,
                                                  downstreamMessageSender ?? new TestDownstreamMessageSender(),
                                                  DateTime.UtcNow.Subtract(startTimeOffset ?? TimeSpan.Zero));
            var effectiveRegion = region ?? TestUtils.TestRegion;
            request.SetRegion(effectiveRegion);
            if (loRaPayload is not null)
                request.SetPayload(loRaPayload);
            if (!useRealTimer)
            {
                var timeWatcher = new TestLoRaOperationTimeWatcher(effectiveRegion, elapsedTimes);
                request.UseTimeWatcher(timeWatcher);
            }

            return request;
        }

        public override void NotifyFailed(string deviceId, LoRaDeviceRequestFailedReason reason, Exception exception = null)
        {
            base.NotifyFailed(deviceId, reason, exception);

            ProcessingFailed = true;
            ProcessingFailedReason = reason;
            this.complete.Release();
        }

        public override void NotifySucceeded(LoRaDevice loRaDevice, DownlinkMessage downlink)
        {
            base.NotifySucceeded(loRaDevice, downlink);

            ResponseDownlink = downlink;
            ProcessingSucceeded = true;
            this.complete.Release();
        }

        public Task<bool> WaitCompleteAsync(int timeout = default)
        {
            if (timeout == default)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    timeout = 1000 * 60;
                }
                else
                {
                    timeout = 10 * 1000;
                }
            }

            return this.complete.WaitAsync(timeout);
        }

        private LoRaOperationTimeWatcher fixTimeWacher;

        internal void UseTimeWatcher(LoRaOperationTimeWatcher timeWatcher) => this.fixTimeWacher = timeWatcher;

        public override LoRaOperationTimeWatcher GetTimeWatcher() => this.fixTimeWacher ?? base.GetTimeWatcher();

        public void Dispose() => this.complete.Dispose();
    }
}
