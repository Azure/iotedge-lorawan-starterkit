// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
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

        public override bool CheckMic(NetworkSessionKey key, uint? server32BitFcnt = null) =>
            throw new NotImplementedException();

        public override bool CheckMic(AppKey key) =>
            Mic == LoRaWan.Mic.ComputeForJoinRequest(key, MHdr, AppEui, DevEUI, DevNonce);

        public override byte[] Serialize(AppSessionKey key) => throw new NotImplementedException("The payload is not encrypted in case of a join message");

        public override byte[] GetByteMessage()
        {
            var messageArray = new byte[MacHeader.Size + JoinEui.Size + DevEui.Size + DevNonce.Size + LoRaWan.Mic.Size];
            var start = 0;
            _ = MHdr.Write(messageArray.AsSpan(start));
            start += MacHeader.Size;
            _ = AppEui.Write(messageArray.AsSpan(start));
            start += JoinEui.Size;
            _ = DevEUI.Write(messageArray.AsSpan(start));
            start += DevEui.Size;
            _ = DevNonce.Write(messageArray.AsSpan(start));
            start += DevNonce.Size;

            if (Mic is { } someMic)
            {
                _ = someMic.Write(messageArray.AsSpan(start));
            }

            return messageArray;
        }

        public override byte[] Serialize(NetworkSessionKey key) => throw new NotImplementedException();
    }
}
