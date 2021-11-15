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
        /// Creates a WaitableLoRaRequest using a real time watcher.
        /// </summary>
        public static WaitableLoRaRequest Create(LoRaPayloadData payload) =>
            new WaitableLoRaRequest(payload);

        /// <summary>
        /// Creates a WaitableLoRaRequest that uses a deterministic time handler.
        /// </summary>
        /// <param name="rxpk">Rxpk instance.</param>
        /// <param name="packetForwarder">PacketForwarder instance.</param>
        /// <param name="startTimeOffset">Is subtracted from the current time to determine the start time for the deterministic time watcher. Default is TimeSpan.Zero.</param>
        /// <param name="constantElapsedTime">Controls how much time is elapsed when querying the time watcher. Default is TimeSpan.Zero.</param>
        /// <param name="useRealTimer">Allows you to opt-in to use a real, non-deterministic time watcher.</param>
        public static WaitableLoRaRequest Create(Rxpk rxpk,
                                                 IPacketForwarder packetForwarder = null,
                                                 TimeSpan? startTimeOffset = null,
                                                 TimeSpan? constantElapsedTime = null,
                                                 bool useRealTimer = false) =>
            Create(rxpk, LoRaEnumerable.Repeat(constantElapsedTime ?? TimeSpan.Zero), packetForwarder, startTimeOffset, useRealTimer);

        /// <summary>
        /// Creates a WaitableLoRaRequest that uses a deterministic time handler.
        /// </summary>
        /// <param name="rxpk">Rxpk instance.</param>
        /// <param name="elapsedTimes">Returns an elapsed time every time it is requested.</param>
        /// <param name="packetForwarder">PacketForwarder instance.</param>
        /// <param name="startTimeOffset">Is subtracted from the current time to determine the start time for the deterministic time watcher. Default is TimeSpan.Zero.</param>
        /// <param name="useRealTimer">Allows you to opt-in to use a real, non-deterministic time watcher.</param>
        public static WaitableLoRaRequest Create(Rxpk rxpk,
                                                 IEnumerable<TimeSpan> elapsedTimes,
                                                 IPacketForwarder packetForwarder = null,
                                                 TimeSpan? startTimeOffset = null,
                                                 bool useRealTimer = false)
        {
            var request = new WaitableLoRaRequest(rxpk,
                                                  packetForwarder ?? new TestPacketForwarder(),
                                                  DateTime.UtcNow.Subtract(startTimeOffset ?? TimeSpan.Zero));

            if (!useRealTimer)
            {
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                if (!RegionManager.TryResolveRegion(rxpk, out var region))
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                    throw new InvalidOperationException("Could not resolve region.");
                var timeWatcher = new TestLoRaOperationTimeWatcher(region, elapsedTimes);
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
