// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class WaitableLoRaRequest : LoRaRequest
    {
        SemaphoreSlim complete;

        public bool ProcessingFailed { get; private set; }

        public LoRaDeviceRequestFailedReason ProcessingFailedReason { get; private set; }

        public DownlinkPktFwdMessage ResponseDownlink { get; private set; }

        public bool ProcessingSucceeded { get; private set; }

        public WaitableLoRaRequest(LoRaPayloadData payload)
            : base(payload)
        {
            this.complete = new SemaphoreSlim(0);
        }

        public WaitableLoRaRequest(Rxpk rxpk, IPacketForwarder packetForwarder)
            : this(rxpk, packetForwarder, DateTime.UtcNow)
        {
        }

        public WaitableLoRaRequest(Rxpk rxpk, IPacketForwarder packetForwarder, DateTime startTime)
            : base(rxpk, packetForwarder, startTime)
        {
            this.complete = new SemaphoreSlim(0);
        }

        public override void NotifyFailed(string deviceId, LoRaDeviceRequestFailedReason reason, Exception exception = null)
        {
            base.NotifyFailed(deviceId, reason, exception);

            this.ProcessingFailed = true;
            this.ProcessingFailedReason = reason;
            this.complete.Release();
        }

        public override void NotifySucceeded(LoRaDevice loRaDevice, DownlinkPktFwdMessage downlink)
        {
            base.NotifySucceeded(loRaDevice, downlink);

            this.ResponseDownlink = downlink;
            this.ProcessingSucceeded = true;
            this.complete.Release();
        }

        internal Task<bool> WaitCompleteAsync(int timeout = default(int))
        {
            if (timeout == default(int))
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

        LoRaOperationTimeWatcher fixTimeWacher;

        internal void UseTimeWatcher(LoRaOperationTimeWatcher timeWatcher) => this.fixTimeWacher = timeWatcher;

        public override LoRaOperationTimeWatcher GetTimeWatcher() => this.fixTimeWacher ?? base.GetTimeWatcher();
    }
}