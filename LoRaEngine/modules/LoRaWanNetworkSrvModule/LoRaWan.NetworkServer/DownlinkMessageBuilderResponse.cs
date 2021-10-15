// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.LoRaPhysical;

    internal class DownlinkMessageBuilderResponse
    {
        internal DownlinkPktFwdMessage DownlinkPktFwdMessage { get; set; }

        internal bool IsMessageTooLong { get; set; }

        internal DownlinkMessageBuilderResponse(DownlinkPktFwdMessage downlinkPktFwdMessage, bool isMessageTooLong)
        {
            DownlinkPktFwdMessage = downlinkPktFwdMessage;
            IsMessageTooLong = isMessageTooLong;
        }
    }
}
