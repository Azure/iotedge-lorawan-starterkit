// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// Implementation of the Join Request message type.
    /// </summary>
    public class LoRaPayloadJoinRequest : LoRaPayload
    {
        /// <summary>
        /// Gets or sets aka JoinEUI
        /// </summary>
        public Memory<byte> AppEUI { get; set; }

        public Memory<byte> DevEUI { get; set; }

        public Memory<byte> DevNonce { get; set; }

        /// <summary>
        /// Gets the value of DevEUI as <see cref="string"/>
        /// </summary>
        public string GetDevEUIAsString() => ConversionHelper.ReverseByteArrayToString(this.DevEUI);

        /// <summary>
        /// Gets the value of AppEUI as <see cref="string"/>
        /// </summary>
        public string GetAppEUIAsString() => ConversionHelper.ReverseByteArrayToString(this.AppEUI);

        /// <summary>
        /// Gets the value <see cref="DevNonce"/> as <see cref="string"/>
        /// </summary>
        public string GetDevNonceAsString() => ConversionHelper.ByteArrayToString(this.DevNonce);

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadJoinRequest"/> class.
        /// Constructor used for the simulator
        /// </summary>
        public LoRaPayloadJoinRequest()
        {
        }

        public LoRaPayloadJoinRequest(byte[] inputMessage)
            : base(inputMessage)
        {
            this.Mhdr = new Memory<byte>(inputMessage, 0, 1);
            // get the joinEUI field
            this.AppEUI = new Memory<byte>(inputMessage, 1, 8);
            // get the DevEUI
            this.DevEUI = new Memory<byte>(inputMessage, 9, 8);
            // get the DevNonce
            this.DevNonce = new Memory<byte>(inputMessage, 17, 2);
        }

        public LoRaPayloadJoinRequest(string appEUI, string devEUI, byte[] devNonce)
        {
            // Mhdr is always 0 in case of a join request
            this.Mhdr = new byte[1] { 0x00 };

            var appEUIBytes = ConversionHelper.StringToByteArray(appEUI);
            var devEUIBytes = ConversionHelper.StringToByteArray(devEUI);

            // Store as reversed value
            // When coming from real device and pktfwd is is reversed
            // message processor reverses both values before getting it
            Array.Reverse(appEUIBytes);
            Array.Reverse(devEUIBytes);
            this.AppEUI = new Memory<byte>(appEUIBytes);
            this.DevEUI = new Memory<byte>(devEUIBytes);
            this.DevNonce = new Memory<byte>(devNonce);
            this.Mic = default(Memory<byte>);
        }

        public override bool CheckMic(string appKey, uint? server32BitFcnt = null)
        {
            return this.Mic.ToArray().SequenceEqual(this.PerformMic(appKey));
        }

        public void SetMic(string appKey)
        {
            this.Mic = this.PerformMic(appKey);
        }

        private byte[] PerformMic(string appKey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");

            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(appKey));
            mac.Init(key);
            var algoInputBytes = new byte[19];
            var algoInput = new Memory<byte>(algoInputBytes);

            var offset = 0;
            this.Mhdr.CopyTo(algoInput);
            offset += this.Mhdr.Length;
            this.AppEUI.CopyTo(algoInput.Slice(offset));
            offset += this.AppEUI.Length;
            this.DevEUI.CopyTo(algoInput.Slice(offset));
            offset += this.DevEUI.Length;
            this.DevNonce.CopyTo(algoInput.Slice(offset));

            mac.BlockUpdate(algoInputBytes, 0, algoInputBytes.Length);

            var result = MacUtilities.DoFinal(mac);
            return result.Take(4).ToArray();
        }

        public override byte[] PerformEncryption(string appSkey) => throw new NotImplementedException("The payload is not encrypted in case of a join message");

        public override byte[] GetByteMessage()
        {
            List<byte> messageArray = new List<byte>(23);
            messageArray.AddRange(this.Mhdr.ToArray());
            messageArray.AddRange(this.AppEUI.ToArray());
            messageArray.AddRange(this.DevEUI.ToArray());
            messageArray.AddRange(this.DevNonce.ToArray());
            if (!this.Mic.Span.IsEmpty)
            {
                messageArray.AddRange(this.Mic.ToArray());
            }

            return messageArray.ToArray();
        }

        /// <summary>
        /// Serializes uplink message, used by simulator
        /// </summary>
        public UplinkPktFwdMessage SerializeUplink(string appKey, string datr = "SF10BW125", double freq = 868.3, uint tmst = 0)
        {
            this.SetMic(appKey);
            return new UplinkPktFwdMessage(this.GetByteMessage(), datr, freq, tmst);
        }
    }
}
