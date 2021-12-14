// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// Implementation of the Join Request message type.
    /// </summary>
    public class LoRaPayloadJoinRequest : LoRaPayload
    {
        /// <summary>
        /// Gets or sets aka JoinEUI.
        /// </summary>
        public Memory<byte> AppEUI { get; set; }

        public Memory<byte> DevEUI { get; set; }

        public DevNonce DevNonce { get; set; }

        /// <summary>
        /// Gets the value of DevEUI as <see cref="string"/>.
        /// </summary>
        public string GetDevEUIAsString() => ConversionHelper.ReverseByteArrayToString(DevEUI);

        /// <summary>
        /// Gets the value of AppEUI as <see cref="string"/>.
        /// </summary>
        public string GetAppEUIAsString() => ConversionHelper.ReverseByteArrayToString(AppEUI);

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadJoinRequest"/> class.
        /// Constructor used for the simulator.
        /// </summary>
        public LoRaPayloadJoinRequest()
        {
        }

        public LoRaPayloadJoinRequest(byte[] inputMessage)
            : base(inputMessage)
        {
            Mhdr = new Memory<byte>(inputMessage, 0, 1);
            // get the joinEUI field
            AppEUI = new Memory<byte>(inputMessage, 1, 8);
            // get the DevEUI
            DevEUI = new Memory<byte>(inputMessage, 9, 8);
            // get the DevNonce
            DevNonce = DevNonce.Read(inputMessage.AsSpan(17));
        }

        public LoRaPayloadJoinRequest(string appEUI, string devEUI, DevNonce devNonce)
        {
            // Mhdr is always 0 in case of a join request
            Mhdr = new byte[1] { 0x00 };

            var appEUIBytes = ConversionHelper.StringToByteArray(appEUI);
            var devEUIBytes = ConversionHelper.StringToByteArray(devEUI);

            // Store as reversed value
            // When coming from real device and pktfwd is is reversed
            // message processor reverses both values before getting it
            Array.Reverse(appEUIBytes);
            Array.Reverse(devEUIBytes);
            AppEUI = new Memory<byte>(appEUIBytes);
            DevEUI = new Memory<byte>(devEUIBytes);
            DevNonce = devNonce;
            Mic = default;
        }

        public override bool CheckMic(string nwskey, uint? server32BitFcnt = null)
        {
            return Mic.ToArray().SequenceEqual(PerformMic(nwskey));
        }

        public void SetMic(string appKey)
        {
            Mic = PerformMic(appKey);
        }

        private byte[] PerformMic(string appKey)
        {
            var mac = MacUtilities.GetMac("AESCMAC");

            var key = new KeyParameter(ConversionHelper.StringToByteArray(appKey));
            mac.Init(key);
            var algoInputBytes = new byte[19];
            var algoInput = new Memory<byte>(algoInputBytes);

            var offset = 0;
            Mhdr.CopyTo(algoInput);
            offset += Mhdr.Length;
            AppEUI.CopyTo(algoInput[offset..]);
            offset += AppEUI.Length;
            DevEUI.CopyTo(algoInput[offset..]);
            offset += DevEUI.Length;
            _ = DevNonce.Write(algoInput[offset..].Span);

            mac.BlockUpdate(algoInputBytes, 0, algoInputBytes.Length);

            var result = MacUtilities.DoFinal(mac);
            return result.Take(4).ToArray();
        }

        public override byte[] PerformEncryption(string appSkey) => throw new NotImplementedException("The payload is not encrypted in case of a join message");

        public override byte[] GetByteMessage()
        {
            var messageArray = new byte[Mhdr.Length + AppEUI.Length + DevEUI.Length + DevNonce.Size + Mic.Length];
            var start = 0;
            Mhdr.Span.CopyTo(messageArray.AsSpan(start));
            start += Mhdr.Length;
            AppEUI.Span.CopyTo(messageArray.AsSpan(start));
            start += AppEUI.Length;
            DevEUI.Span.CopyTo(messageArray.AsSpan(start));
            start += DevEUI.Length;
            _ = DevNonce.Write(messageArray.AsSpan(start));
            start += DevNonce.Size;
            if (!Mic.Span.IsEmpty)
            {
                Mic.Span.CopyTo(messageArray.AsSpan(start));
            }

            return messageArray;
        }

        /// <summary>
        /// Serializes uplink message, used by simulator.
        /// </summary>
        public UplinkPktFwdMessage SerializeUplink(string appKey, string datr = "SF10BW125", double freq = 868.3, uint tmst = 0)
        {
            SetMic(appKey);
            return new UplinkPktFwdMessage(GetByteMessage(), datr, freq, tmst);
        }
    }
}
