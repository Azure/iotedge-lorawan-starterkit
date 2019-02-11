﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;

    public class LoRaRequest
    {
        public virtual Rxpk Rxpk { get; }

        public virtual LoRaPayload Payload { get; private set; }

        public virtual IPacketForwarder PacketForwarder { get; }

        public virtual DateTime StartTime { get; }

        public virtual Region LoRaRegion { get; private set; }

        protected LoRaRequest()
        {
        }

        protected LoRaRequest(LoRaPayload payload)
        {
            this.Payload = payload;
        }

        public LoRaRequest(
            Rxpk rxpk,
            IPacketForwarder packetForwarder,
            DateTime startTime)
        {
            this.Rxpk = rxpk;
            this.PacketForwarder = packetForwarder;
            this.StartTime = startTime;
        }

        internal void NotifyFailed(LoRaDevice loRaDevice, Exception error) => this.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ApplicationError, error);

        public virtual void NotifyFailed(LoRaDevice loRaDevice, LoRaDeviceRequestFailedReason reason, Exception exception = null)
        {
        }

        public virtual void NotifySucceeded(LoRaDevice loRaDevice, DownlinkPktFwdMessage downlink)
        {
        }

        internal void SetPayload(LoRaPayload loRaPayload) => this.Payload = loRaPayload;

        internal void SetRegion(Region loRaRegion) => this.LoRaRegion = loRaRegion;
    }
}