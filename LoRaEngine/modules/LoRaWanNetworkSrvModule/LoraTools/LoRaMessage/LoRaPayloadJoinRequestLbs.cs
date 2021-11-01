// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using LoRaWan;

    public class LoRaPayloadJoinRequestLbs : LoRaPayloadJoinRequest
    {
        public LoRaPayloadJoinRequestLbs(MacHeader mhdr,
                                         JoinEui joinEui,
                                         DevEui devEui,
                                         DevNonce devNonce,
                                         Mic mic)
        {
            Mhdr = new byte[1];
            _ = mhdr.Write(Mhdr.Span);

            AppEUI = new byte[JoinEui.Size];
            _ = joinEui.Write(AppEUI.Span);

            DevEUI = new byte[DevEui.Size];
            _ = devEui.Write(DevEUI.Span);

            DevNonce = new byte[LoRaWan.DevNonce.Size];
            _ = devNonce.Write(DevNonce.Span);

            Mic = mic.AsByteArray();
        }
    }
}
