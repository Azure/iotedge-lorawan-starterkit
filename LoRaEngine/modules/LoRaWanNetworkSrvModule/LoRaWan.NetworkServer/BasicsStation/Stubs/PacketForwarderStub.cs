// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.Stubs
{
    using LoRaTools.LoRaPhysical;
    using System;
    using System.Threading.Tasks;

    public class PacketForwarderStub : IPacketForwarder
    {
        public Task SendDownstreamAsync(DownlinkPktFwdMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
