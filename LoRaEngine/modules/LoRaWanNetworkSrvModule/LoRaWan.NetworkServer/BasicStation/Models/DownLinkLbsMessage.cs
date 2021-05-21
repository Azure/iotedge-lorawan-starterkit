// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    class DownLinkLbsMessage
    {
        internal LbsClassADownlink DownlinkLbsMessage { get; set; }

        internal bool IsMessageTooLong { get; set; }

        internal DownLinkLbsMessage(LbsClassADownlink downlinkPktFwdMessage, bool isMessageTooLong)
        {
            this.DownlinkLbsMessage = downlinkPktFwdMessage;
            this.IsMessageTooLong = isMessageTooLong;
        }
    }
}
