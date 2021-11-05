// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using LoRaWan;

    public class LoRaPayloadJoinRequestLbs : LoRaPayloadJoinRequest
    {
        public LoRaPayloadJoinRequestLbs(MacHeader macHeader,
                                         JoinEui joinEui,
                                         DevEui devEui,
                                         DevNonce devNonce,
                                         Mic mic)
        {
            Mhdr = new byte[MacHeader.Size];
            _ = macHeader.Write(Mhdr.Span);

            AppEUI = new byte[JoinEui.Size];
            _ = joinEui.Write(AppEUI.Span);

            DevEUI = new byte[DevEui.Size];
            _ = devEui.Write(DevEUI.Span);

            DevNonce = new byte[LoRaWan.DevNonce.Size];
            _ = devNonce.Write(DevNonce.Span);

            Mic = new byte[LoRaWan.Mic.Size];
            _ = mic.Write(Mic.Span);
        }
    }
}
