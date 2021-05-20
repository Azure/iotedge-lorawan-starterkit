// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer.BasicStation.Models;
    using LoRaWan.NetworkServer.PacketForwarder;

    public class LoRaLbsProcessingRequest : LoRaRequest
    {
        public virtual LnsDataFrameRequest DataFrame { get; set; }

        public virtual LbsDownStreamSender Sender { get; }

        public LoRaLbsProcessingRequest()
        {
        }
    }
}