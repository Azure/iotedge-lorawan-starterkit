// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;

    public sealed class WaitableLoRaRequest : LoRaRequest, IDisposable
    {
        private readonly SemaphoreSlim complete = new SemaphoreSlim(0);

        public bool ProcessingFailed { get; private set; }

        public LoRaDeviceRequestFailedReason ProcessingFailedReason { get; private set; }

        public DownlinkPktFwdMessage ResponseDownlink { get; private set; }

        public bool ProcessingSucceeded { get; private set; }

        private WaitableLoRaRequest(LoRaPayloadData payload)
            : base(payload)
        { }

        private WaitableLoRaRequest(Rxpk rxpk, IPacketForwarder packetForwarder, DateTime startTime)
            : base(rxpk, packetForwarder, startTime)
        { }

        /// <summary>
        /// Creates a Waitable LoRa request using a real time watcher.
        /// </summary>
        public static WaitableLoRaRequest Create(LoRaPayloadData payload) =>
            new WaitableLoRaRequest(payload);

        public static WaitableLoRaRequest Create(Rxpk rxpk,
                                                 IPacketForwarder packetForwarder = null,
                                                 TimeSpan? startTimeOffset = null,
                                                 TimeSpan? constantElapsedTime = null,
                                                 bool useRealTimer = false)
        {
            var requestStartTime = startTimeOffset.HasValue ? DateTime.UtcNow.Subtract(startTimeOffset.Value) : DateTime.UtcNow;
            var request = new WaitableLoRaRequest(rxpk,
                                                  packetForwarder ?? new TestPacketForwarder(),
                                                  requestStartTime);

            if (!useRealTimer)
            {
                constantElapsedTime ??= TimeSpan.Zero;
                Debug.Assert(RegionManager.TryResolveRegion(rxpk, out var region));
                var timeWatcher = new TestLoRaOperationTimeWatcher(region, constantElapsedTime.Value);
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
