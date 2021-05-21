// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer.PacketForwarder;

    public class LoRaPktFwdRequest : LoRaRequest
    {
        public virtual Rxpk Rxpk { get; }

        public virtual IPacketForwarder PacketForwarder { get; }

        public LoRaPktFwdRequest()
        {
        }

        public LoRaPktFwdRequest(
             Rxpk rxpk,
             IPacketForwarder packetForwarder,
             DateTime startTime)
            : base(startTime)
        {
            this.Rxpk = rxpk;
            this.PacketForwarder = packetForwarder;
        }

        public LoRaPktFwdRequest(LoRaPayload payload)
            : base(payload)
        {
        }
    }
}