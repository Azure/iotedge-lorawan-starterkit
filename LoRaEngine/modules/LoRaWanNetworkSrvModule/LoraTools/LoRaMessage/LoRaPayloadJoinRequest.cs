// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using LoRaTools.Utils;
    using LoRaWan;

    /// <summary>
    /// Implementation of the Join Request message type.
    /// </summary>
    public class LoRaPayloadJoinRequest : LoRaPayload
    {
        /// <summary>
        /// Gets or sets aka JoinEUI.
        /// </summary>
        public JoinEui AppEUI { get; set; }

        public Memory<byte> DevEUI { get; set; }

        public DevNonce DevNonce { get; set; }

        /// <summary>
        /// Gets the value of DevEUI as <see cref="string"/>.
        /// </summary>
        public string GetDevEUIAsString() => ConversionHelper.ReverseByteArrayToString(DevEUI);

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadJoinRequest"/> class.
        /// Constructor used for the simulator.
        /// </summary>
        public LoRaPayloadJoinRequest()
        {
        }

        /// <summary>
        /// Constructor used for test code only.
        /// </summary>
        internal LoRaPayloadJoinRequest(JoinEui joinEui, string devEUI, DevNonce devNonce, AppKey key)
        {
            // Mhdr is always 0 in case of a join request
            MHdr = new MacHeader(MacMessageType.JoinRequest);

            var devEUIBytes = ConversionHelper.StringToByteArray(devEUI);

            // Store as reversed value
            // When coming from real device is is reversed
            // message processor reverses both values before getting it
            Array.Reverse(devEUIBytes);
            AppEUI = joinEui;
            DevEUI = new Memory<byte>(devEUIBytes);
            DevNonce = devNonce;
            Mic = PerformMic(key);
        }

        public override bool CheckMic(NetworkSessionKey key, uint? server32BitFcnt = null) =>
            throw new NotImplementedException();

        public override bool CheckMic(AppKey key) => Mic == PerformMic(key);

        private Mic PerformMic(AppKey key)
        {
            var devEui = DevEui.Read(DevEUI.Span);
            return LoRaWan.Mic.ComputeForJoinRequest(key, MHdr, AppEUI, devEui, DevNonce);
        }

        public override byte[] Serialize(AppSessionKey key) => throw new NotImplementedException("The payload is not encrypted in case of a join message");

        public override byte[] GetByteMessage()
        {
            var messageArray = new byte[MacHeader.Size + JoinEui.Size + DevEUI.Length + DevNonce.Size + LoRaWan.Mic.Size];
            var start = 0;
            _ = MHdr.Write(messageArray.AsSpan(start));
            start += MacHeader.Size;
            _ = AppEUI.Write(messageArray.AsSpan(start));
            start += JoinEui.Size;
            DevEUI.Span.CopyTo(messageArray.AsSpan(start));
            start += DevEUI.Length;
            _ = DevNonce.Write(messageArray.AsSpan(start));
            start += DevNonce.Size;

            if (Mic is { } someMic)
            {
                _ = someMic.Write(messageArray.AsSpan(start));
            }

            return messageArray;
        }

        public override byte[] Serialize(NetworkSessionKey key) => throw new NotImplementedException();

        public override byte[] PerformEncryption(AppKey key) => throw new NotImplementedException();
    }
}
