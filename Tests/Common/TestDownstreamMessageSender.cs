// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;
    using LoRaWan.NetworkServer;

    public class TestDownstreamMessageSender : IDownstreamMessageSender
    {
        public IList<DownlinkMessage> DownlinkMessages { get; }

        public TestDownstreamMessageSender()
        {
            DownlinkMessages = new List<DownlinkMessage>();
        }

        public Task SendDownstreamAsync(DownlinkMessage message)
        {
            DownlinkMessages.Add(message);
            return Task.FromResult(0);
        }
    }
}
