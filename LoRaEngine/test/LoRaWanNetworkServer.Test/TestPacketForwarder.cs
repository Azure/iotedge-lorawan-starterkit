// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;
    using LoRaWan.NetworkServer;

    public class TestPacketForwarder : IPacketForwarder
    {
        public List<DownlinkPktFwdMessage> DownlinkMessages { get; }

        public TestPacketForwarder()
        {
            this.DownlinkMessages = new List<DownlinkPktFwdMessage>();
        }

        public Task SendDownstreamAsync(DownlinkPktFwdMessage message)
        {
            this.DownlinkMessages.Add(message);
            return Task.FromResult(0);
        }
    }
}