// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Linq;
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

        public LoRaPayloadJoinRequest(string appEUI, string devEUI, DevNonce devNonce)
        {
            // Mhdr is always 0 in case of a join request
            MHdr = new MacHeader(MacMessageType.JoinRequest);

            var appEUIBytes = ConversionHelper.StringToByteArray(appEUI);
            var devEUIBytes = ConversionHelper.StringToByteArray(devEUI);

            // Store as reversed value
            // When coming from real device is is reversed
            // message processor reverses both values before getting it
            Array.Reverse(appEUIBytes);
            Array.Reverse(devEUIBytes);
            AppEUI = new Memory<byte>(appEUIBytes);
            DevEUI = new Memory<byte>(devEUIBytes);
            DevNonce = devNonce;
            Mic = default;
        }

        public override bool CheckMic(NetworkSessionKey key, uint? server32BitFcnt = null)
        {
            return Mic.ToArray().SequenceEqual(PerformMic(key));
        }

        public override bool CheckMic(AppKey key) => Mic.ToArray().SequenceEqual(PerformMic(key));

        public void SetMic(AppKey appKey)
        {
            Mic = PerformMic(appKey);
        }

        private byte[] PerformMic(NetworkSessionKey key)
        {
            var rawKey = new byte[NetworkSessionKey.Size];
            _ = key.Write(rawKey);
            return PerformMic(rawKey);
        }

        private byte[] PerformMic(AppKey key)
        {
            var rawKey = new byte[AppKey.Size];
            _ = key.Write(rawKey);
            return PerformMic(rawKey);
        }

        private byte[] PerformMic(byte[] rawKey)
        {
            var mac = MacUtilities.GetMac("AESCMAC");

            var key = new KeyParameter(rawKey);
            mac.Init(key);
            var algoInputBytes = new byte[19];
            var algoInput = new Memory<byte>(algoInputBytes);

            var offset = 0;
            _ = MHdr.Write(algoInput.Span);
            offset += MacHeader.Size;
            AppEUI.CopyTo(algoInput[offset..]);
            offset += AppEUI.Length;
            DevEUI.CopyTo(algoInput[offset..]);
            offset += DevEUI.Length;
            _ = DevNonce.Write(algoInput[offset..].Span);

            mac.BlockUpdate(algoInputBytes, 0, algoInputBytes.Length);

            var result = MacUtilities.DoFinal(mac);
            return result.Take(4).ToArray();
        }

        public override byte[] Serialize(AppSessionKey key) => throw new NotImplementedException("The payload is not encrypted in case of a join message");

        public override byte[] GetByteMessage()
        {
            var messageArray = new byte[MacHeader.Size + AppEUI.Length + DevEUI.Length + DevNonce.Size + Mic.Length];
            var start = 0;
            _ = MHdr.Write(messageArray.AsSpan(start));
            start += MacHeader.Size;
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

        public override byte[] Serialize(NetworkSessionKey key) => throw new NotImplementedException();

        public override byte[] PerformEncryption(AppKey key) => throw new NotImplementedException();
    }
}
