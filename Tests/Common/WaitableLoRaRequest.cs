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

        public DownlinkPktFwdMessage ResponseDownlink { get; private set; }

        public bool ProcessingSucceeded { get; private set; }

        private WaitableLoRaRequest(LoRaPayload payload)
            : base(payload)
        { }

        private WaitableLoRaRequest(RadioMetadata radioMetadata, IPacketForwarder packetForwarder, DateTime startTime)
            : base(new BasicStationToRxpk(radioMetadata, RegionManager.EU868), packetForwarder, startTime)
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
        /// <param name="packetForwarder">PacketForwarder instance.</param>
        /// <param name="startTimeOffset">Is subtracted from the current time to determine the start time for the deterministic time watcher. Default is TimeSpan.Zero.</param>
        /// <param name="constantElapsedTime">Controls how much time is elapsed when querying the time watcher. Default is TimeSpan.Zero.</param>
        /// <param name="useRealTimer">Allows you to opt-in to use a real, non-deterministic time watcher.</param>
        public static WaitableLoRaRequest Create(RadioMetadata radioMetadata,
                                                 LoRaPayload loRaPayload ,
                                                 IPacketForwarder packetForwarder = null,
                                                 TimeSpan? startTimeOffset = null,
                                                 TimeSpan? constantElapsedTime = null,
                                                 bool useRealTimer = false) =>
            Create(radioMetadata, LoRaEnumerable.RepeatInfinite(constantElapsedTime ?? TimeSpan.Zero), packetForwarder, startTimeOffset, useRealTimer, loRaPayload);

        /// <summary>
        /// Creates a WwaitableLoRaRequest that is configured to miss certain receive windows.
        /// </summary>
        /// <param name="rxpk">Rxpk instance.</param>
        /// <param name="packetForwarder">PacketForwarder instance.</param>
        /// <param name="inTimeForC2DMessageCheck">If set to true it ensures that processing is fast enough that C2D messages can be checked.</param>
        /// <param name="inTimeForAdditionalMessageCheck">If set to true it ensures that processing is fast enough that additional C2D messages can be checked.</param>
        /// <param name="inTimeForDownlinkDelivery">If set to true it ensures that processing is fast enough that C2D messages can be checked.</param>
        public static WaitableLoRaRequest Create(RadioMetadata radioMetadata, IPacketForwarder packetForwarder,
                                                 bool inTimeForC2DMessageCheck,
                                                 bool inTimeForAdditionalMessageCheck,
                                                 bool inTimeForDownlinkDelivery,
                                                 LoRaPayloadData loRaPayloadData = null)
        {
            var c2dMessageCheckTimeSpan = inTimeForC2DMessageCheck ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(10);
            var additionalMessageCheckTimeSpan = inTimeForAdditionalMessageCheck ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(10);
            var downlinkDeliveryTimeSpan = inTimeForDownlinkDelivery ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(10);
            return Create(radioMetadata, new[] { c2dMessageCheckTimeSpan, c2dMessageCheckTimeSpan, additionalMessageCheckTimeSpan, downlinkDeliveryTimeSpan }, packetForwarder: packetForwarder, loRaPayload: loRaPayloadData);
        }

        private static WaitableLoRaRequest Create(RadioMetadata radioMetadata,
                                                  IEnumerable<TimeSpan> elapsedTimes,
                                                  IPacketForwarder packetForwarder = null,
                                                  TimeSpan? startTimeOffset = null,
                                                  bool useRealTimer = false,
                                                  LoRaPayload loRaPayload = null)
        {
            var request = new WaitableLoRaRequest(radioMetadata,
                                                  packetForwarder ?? new TestPacketForwarder(),
                                                  DateTime.UtcNow.Subtract(startTimeOffset ?? TimeSpan.Zero));
            if (loRaPayload is not null)
                request.SetPayload(loRaPayload);
            if (!useRealTimer)
            {
                var timeWatcher = new TestLoRaOperationTimeWatcher(RegionManager.EU868, elapsedTimes);
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

        public override void NotifySucceeded(LoRaDevice loRaDevice, DownlinkPktFwdMessage downlink)
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
