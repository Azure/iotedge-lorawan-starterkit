// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using LoRaWan;

    public class LoRaPayloadJoinRequestLns : LoRaPayloadJoinRequest
    {
        public LoRaPayloadJoinRequestLns(MacHeader macHeader,
                                         JoinEui joinEui,
                                         DevEui devEui,
                                         DevNonce devNonce,
                                         Mic mic)
        {
            MHdr = macHeader;
            AppEUI = joinEui;

            DevEUI = new byte[DevEui.Size];
            _ = devEui.Write(DevEUI.Span);

            DevNonce = devNonce;

            Mic = mic;
        }
    }
}
