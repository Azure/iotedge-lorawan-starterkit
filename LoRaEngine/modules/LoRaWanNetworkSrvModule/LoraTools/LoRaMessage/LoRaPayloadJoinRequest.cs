// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using LoRaWan;

    /// <summary>
    /// Implementation of the Join Request message type.
    /// </summary>
    public class LoRaPayloadJoinRequest : LoRaPayload
    {
        /// <summary>
        /// Gets or sets aka JoinEUI.
        /// </summary>
        public JoinEui AppEui { get; set; }

        public DevEui DevEUI { get; set; }

        public DevNonce DevNonce { get; set; }

        public LoRaPayloadJoinRequest(JoinEui joinEui, DevEui devEui, DevNonce devNonce, Mic mic)
        {
            MHdr = new MacHeader(MacMessageType.JoinRequest);
            AppEui = joinEui;
            DevEUI = devEui;
            DevNonce = devNonce;
            Mic = mic;
        }

        public bool CheckMic(AppKey key) =>
            Mic == LoRaWan.Mic.ComputeForJoinRequest(key, MHdr, AppEui, DevEUI, DevNonce);

    }
}
