// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;

    // Packet forwarder
    public interface IPacketForwarder
    {
        /// <summary>
        /// Send downstream message to LoRa device
        /// </summary>
        Task SendDownstreamAsync(DownlinkPktFwdMessage message);
    }
}