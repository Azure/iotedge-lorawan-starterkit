// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Org.BouncyCastle.Crypto.Engines;
    using Org.BouncyCastle.Crypto.Parameters;

    /// <summary>
    /// Implementation of a LoRa Join-Accept frame
    /// </summary>
    public class LoRaPayloadJoinAccept : LoRaPayload
    {
        const ushort MaxRxDelayValue = 16;

        /// <summary>
        /// Gets or sets server Nonce aka JoinNonce
        /// </summary>
        public Memory<byte> AppNonce { get; set; }

        /// <summary>
        /// Gets or sets device home network aka Home_NetId
        /// </summary>
        public Memory<byte> NetID { get; set; }

        /// <summary>
        /// Gets or sets dLSettings
        /// </summary>
        public Memory<byte> DlSettings { get; set; }

        /// <summary>
        /// Gets or sets rxDelay
        /// </summary>
        public Memory<byte> RxDelay { get; set; }

        /// <summary>
        /// Gets or sets cFList / Optional
        /// </summary>
        public Memory<byte> CfList { get; set; }

        /// <summary>
        /// Gets or sets frame Counter
        /// </summary>
        public Memory<byte> Fcnt { get; set; }

        public int Rx1DrOffset => (this.DlSettings.Span[0] >> 4) & 0b00000111;

        public int Rx2Dr => this.DlSettings.Span[0] & 0b00001111;

        public LoRaPayloadJoinAccept(string netId, byte[] devAddr, byte[] appNonce, byte[] dlSettings, uint rxDelayValue, byte[] cfList)
        {
            byte[] rxDelay = new byte[1];
            if (rxDelayValue >= 0 && rxDelayValue < MaxRxDelayValue)
            {
                rxDelay[0] = (byte)rxDelayValue;
            }

            int cfListLength = cfList == null ? 0 : cfList.Length;
            this.RawMessage = new byte[1 + 12 + cfListLength];
            this.Mhdr = new Memory<byte>(this.RawMessage, 0, 1);
            Array.Copy(new byte[] { 32 }, 0, this.RawMessage, 0, 1);
            this.AppNonce = new Memory<byte>(this.RawMessage, 1, 3);
            Array.Copy(appNonce, 0, this.RawMessage, 1, 3);
            this.NetID = new Memory<byte>(this.RawMessage, 4, 3);
            Array.Copy(ConversionHelper.StringToByteArray(netId), 0, this.RawMessage, 4, 3);
            this.DevAddr = new Memory<byte>(this.RawMessage, 7, 4);
            Array.Copy(devAddr, 0, this.RawMessage, 7, 4);
            this.DlSettings = new Memory<byte>(this.RawMessage, 11, 1);
            Array.Copy(dlSettings, 0, this.RawMessage, 11, 1);
            this.RxDelay = new Memory<byte>(this.RawMessage, 12, 1);
            Array.Copy(rxDelay, 0, this.RawMessage, 12, 1);
            // set payload Wrapper fields
            if (cfListLength > 0)
            {
                this.CfList = new Memory<byte>(this.RawMessage, 13, cfListLength);
                Array.Copy(cfList, 0, this.RawMessage, 13, cfListLength);
            }

            // cfList = StringToByteArray("184F84E85684B85E84886684586E8400");
            this.Fcnt = BitConverter.GetBytes(0x01);
            if (BitConverter.IsLittleEndian)
            {
                this.AppNonce.Span.Reverse();
                this.NetID.Span.Reverse();
                this.DevAddr.Span.Reverse();
                this.DlSettings.Span.Reverse();
                this.RxDelay.Span.Reverse();
            }

            var algoinput = this.Mhdr.ToArray().Concat(this.AppNonce.ToArray()).Concat(this.NetID.ToArray()).Concat(this.DevAddr.ToArray()).Concat(this.DlSettings.ToArray()).Concat(this.RxDelay.ToArray()).ToArray();
            if (!this.CfList.Span.IsEmpty)
                algoinput = algoinput.Concat(this.CfList.ToArray()).ToArray();
        }

        public LoRaPayloadJoinAccept(byte[] inputMessage, string appKey)
        {
            // Only MHDR is not encrypted with the key
            // ( PHYPayload = MHDR[1] | MACPayload[..] | MIC[4] )
            this.Mhdr = new Memory<byte>(inputMessage, 0, 1);
            // Then we will take the rest and decrypt it
            // DecryptPayload(inputMessage);
            // var decrypted = PerformEncryption(appKey);
            // Array.Copy(decrypted, 0, inputMessage, 0, decrypted.Length);
            // DecryptPayload(inputMessage);
            AesEngine aesEngine = new AesEngine();
            var key = ConversionHelper.StringToByteArray(appKey);
            aesEngine.Init(true, new KeyParameter(key));
            Aes aes = new AesManaged
            {
                Key = key,
                IV = new byte[16],
                Mode = CipherMode.ECB,
                Padding = PaddingMode.None
            };

            ICryptoTransform cipher;

            cipher = aes.CreateEncryptor();
            byte[] pt = new byte[inputMessage.Length - 1];
            Array.Copy(inputMessage, 1, pt, 0, pt.Length);
            // Array.Reverse(pt);
            var decryptedPayload = cipher.TransformFinalBlock(pt, 0, pt.Length);
            // We will copy back in the main inputMessage the content
            Array.Copy(decryptedPayload, 0, inputMessage, 1, decryptedPayload.Length);
            // ( MACPayload = AppNonce[3] | NetID[3] | DevAddr[4] | DLSettings[1] | RxDelay[1] | CFList[0|15] )
            var appNonce = new byte[3];
            Array.Copy(inputMessage, 1, appNonce, 0, 3);
            Array.Reverse(appNonce);
            this.AppNonce = new Memory<byte>(appNonce);
            var netID = new byte[3];
            Array.Copy(inputMessage, 4, netID, 0, 3);
            Array.Reverse(netID);
            this.NetID = new Memory<byte>(netID);
            var devAddr = new byte[4];
            Array.Copy(inputMessage, 7, devAddr, 0, 4);
            Array.Reverse(devAddr);
            this.DevAddr = new Memory<byte>(devAddr);
            var dlSettings = new byte[1];
            Array.Copy(inputMessage, 11, dlSettings, 0, 1);
            this.DlSettings = new Memory<byte>(dlSettings);
            var rxDelay = new byte[1];
            Array.Copy(inputMessage, 12, rxDelay, 0, 1);
            this.RxDelay = new Memory<byte>(rxDelay);
            // It's the configuration list, it can be empty or up to 15 bytes
            // - 17 = - 1 - 3 - 3 - 4 - 1 - 1 - 4
            // This is the size of all mandatory elements of the message
            var cfList = new byte[inputMessage.Length - 17];
            Array.Copy(inputMessage, 12, cfList, 0, inputMessage.Length - 17);
            Array.Reverse(cfList);
            this.CfList = new Memory<byte>(cfList);
            var mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            this.Mic = new Memory<byte>(mic);
        }

        public override byte[] PerformEncryption(string appSkey)
        {
            byte[] pt;
            if (!this.CfList.Span.IsEmpty)
            {
                pt = this.AppNonce.ToArray().Concat(this.NetID.ToArray()).Concat(this.DevAddr.ToArray()).Concat(this.DlSettings.ToArray()).Concat(this.RxDelay.ToArray()).Concat(this.CfList.ToArray()).Concat(this.Mic.ToArray()).ToArray();
            }
            else
            {
                pt = this.AppNonce.ToArray().Concat(this.NetID.ToArray()).Concat(this.DevAddr.ToArray()).Concat(this.DlSettings.ToArray()).Concat(this.RxDelay.ToArray()).Concat(this.Mic.ToArray()).ToArray();
            }

            Aes aes = new AesManaged
            {
                Key = ConversionHelper.StringToByteArray(appSkey),
                IV = new byte[16],
                Mode = CipherMode.ECB,
                Padding = PaddingMode.None
            };

            ICryptoTransform cipher;

            cipher = aes.CreateDecryptor();
            var encryptedPayload = cipher.TransformFinalBlock(pt, 0, pt.Length);
            this.RawMessage = new byte[encryptedPayload.Length];
            Array.Copy(encryptedPayload, 0, this.RawMessage, 0, encryptedPayload.Length);
            return encryptedPayload;
        }

        public override byte[] GetByteMessage()
        {
            List<byte> messageArray = new List<byte>();
            messageArray.AddRange(this.Mhdr.ToArray());
            messageArray.AddRange(this.RawMessage);

            return messageArray.ToArray();
        }

        public override bool CheckMic(string nwskey, uint? server32BitFcnt = null)
        {
            throw new NotImplementedException();
        }

        public DownlinkPktFwdMessage Serialize(string appKey, string datr, double freq, long tmst, string devEUI)
        {
            var algoinput = this.Mhdr.ToArray().Concat(this.AppNonce.ToArray()).Concat(this.NetID.ToArray()).Concat(this.DevAddr.ToArray()).Concat(this.DlSettings.ToArray()).Concat(this.RxDelay.ToArray()).ToArray();
            if (!this.CfList.Span.IsEmpty)
                algoinput = algoinput.Concat(this.CfList.ToArray()).ToArray();

            this.CalculateMic(appKey, algoinput);
            this.PerformEncryption(appKey);

            return new DownlinkPktFwdMessage(this.GetByteMessage(), datr, freq, tmst);
        }
    }
}
