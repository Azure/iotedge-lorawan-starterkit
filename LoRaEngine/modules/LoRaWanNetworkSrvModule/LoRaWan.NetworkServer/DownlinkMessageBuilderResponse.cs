// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.LoRaPhysical;

    public class DownlinkMessageBuilderResponse
    {
        internal DownlinkBasicsStationMessage DownlinkMessage { get; set; }

        internal bool IsMessageTooLong { get; set; }

        public int ReceiveWindow { get; }

        internal DownlinkMessageBuilderResponse(DownlinkBasicsStationMessage downlinkPktFwdMessage, bool isMessageTooLong, int receiveWindow)
        {
            DownlinkMessage = downlinkPktFwdMessage;
            IsMessageTooLong = isMessageTooLong;
            ReceiveWindow = receiveWindow;
        }
    }
}
