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
    using Org.BouncyCastle.Crypto.Engines;
    using Org.BouncyCastle.Crypto.Parameters;

    /// <summary>
    /// Implementation of a LoRa Join-Accept frame.
    /// </summary>
    public class LoRaPayloadJoinAccept : LoRaPayload
    {
        private const ushort MaxRxDelayValue = 16;

        /// <summary>
        /// Gets or sets server Nonce aka JoinNonce.
        /// </summary>
        public Memory<byte> AppNonce { get; set; }

        /// <summary>
        /// Gets or sets device home network aka Home_NetId.
        /// </summary>
        public Memory<byte> NetID { get; set; }

        /// <summary>
        /// Gets or sets dLSettings.
        /// </summary>
        public Memory<byte> DlSettings { get; set; }

        /// <summary>
        /// Gets or sets rxDelay.
        /// </summary>
        public Memory<byte> RxDelay { get; set; }

        /// <summary>
        /// Gets or sets cFList / Optional.
        /// </summary>
        public Memory<byte> CfList { get; set; }

        /// <summary>
        /// Gets or sets frame Counter.
        /// </summary>
        public Memory<byte> Fcnt { get; set; }

        public int Rx1DrOffset => (DlSettings.Span[0] >> 4) & 0b00000111;

        public int Rx2Dr => DlSettings.Span[0] & 0b00001111;

        /// Constructor needed for mocking
        public LoRaPayloadJoinAccept()
        { }

        public LoRaPayloadJoinAccept(string netId, byte[] devAddr, byte[] appNonce, byte[] dlSettings, uint rxDelayValue, byte[] cfList)
        {
            var rxDelay = new byte[1];
            if (rxDelayValue is >= 0 and < MaxRxDelayValue)
            {
                rxDelay[0] = (byte)rxDelayValue;
            }

            var cfListLength = cfList == null ? 0 : cfList.Length;
            RawMessage = new byte[1 + 12 + cfListLength];
            Mhdr = new Memory<byte>(RawMessage, 0, 1);
            Array.Copy(new byte[] { 32 }, 0, RawMessage, 0, 1);
            AppNonce = new Memory<byte>(RawMessage, 1, 3);
            Array.Copy(appNonce, 0, RawMessage, 1, 3);
            NetID = new Memory<byte>(RawMessage, 4, 3);
            Array.Copy(ConversionHelper.StringToByteArray(netId), 0, RawMessage, 4, 3);
            DevAddr = new Memory<byte>(RawMessage, 7, 4);
            Array.Copy(devAddr, 0, RawMessage, 7, 4);
            DlSettings = new Memory<byte>(RawMessage, 11, 1);
            Array.Copy(dlSettings, 0, RawMessage, 11, 1);
            RxDelay = new Memory<byte>(RawMessage, 12, 1);
            Array.Copy(rxDelay, 0, RawMessage, 12, 1);
            // set payload Wrapper fields
            if (cfListLength > 0)
            {
                CfList = new Memory<byte>(RawMessage, 13, cfListLength);
                Array.Copy(cfList, 0, RawMessage, 13, cfListLength);
            }

            // cfList = StringToByteArray("184F84E85684B85E84886684586E8400");
            Fcnt = BitConverter.GetBytes(0x01);
            if (BitConverter.IsLittleEndian)
            {
                AppNonce.Span.Reverse();
                NetID.Span.Reverse();
                DevAddr.Span.Reverse();
                DlSettings.Span.Reverse();
                RxDelay.Span.Reverse();
            }
        }

        public LoRaPayloadJoinAccept(byte[] inputMessage, string appKey)
        {
            if (inputMessage is null) throw new ArgumentNullException(nameof(inputMessage));

            // Only MHDR is not encrypted with the key
            // ( PHYPayload = MHDR[1] | MACPayload[..] | MIC[4] )
            Mhdr = new Memory<byte>(inputMessage, 0, 1);
            // Then we will take the rest and decrypt it
            // DecryptPayload(inputMessage);
            // var decrypted = PerformEncryption(appKey);
            // Array.Copy(decrypted, 0, inputMessage, 0, decrypted.Length);
            // DecryptPayload(inputMessage);
            var aesEngine = new AesEngine();
            var key = ConversionHelper.StringToByteArray(appKey);
            aesEngine.Init(true, new KeyParameter(key));
            using var aes = Aes.Create("AesManaged");
            aes.Key = key;
            aes.IV = new byte[16];
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            ICryptoTransform cipher;

#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            // Part of the LoRaWAN specification
            cipher = aes.CreateEncryptor();
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV
            var pt = new byte[inputMessage.Length - 1];
            Array.Copy(inputMessage, 1, pt, 0, pt.Length);
            // Array.Reverse(pt);
            var decryptedPayload = cipher.TransformFinalBlock(pt, 0, pt.Length);
            // We will copy back in the main inputMessage the content
            Array.Copy(decryptedPayload, 0, inputMessage, 1, decryptedPayload.Length);
            // ( MACPayload = AppNonce[3] | NetID[3] | DevAddr[4] | DLSettings[1] | RxDelay[1] | CFList[0|15] )
            var appNonce = new byte[3];
            Array.Copy(inputMessage, 1, appNonce, 0, 3);
            Array.Reverse(appNonce);
            AppNonce = new Memory<byte>(appNonce);
            var netID = new byte[3];
            Array.Copy(inputMessage, 4, netID, 0, 3);
            Array.Reverse(netID);
            NetID = new Memory<byte>(netID);
            var devAddr = new byte[4];
            Array.Copy(inputMessage, 7, devAddr, 0, 4);
            Array.Reverse(devAddr);
            DevAddr = new Memory<byte>(devAddr);
            var dlSettings = new byte[1];
            Array.Copy(inputMessage, 11, dlSettings, 0, 1);
            DlSettings = new Memory<byte>(dlSettings);
            var rxDelay = new byte[1];
            Array.Copy(inputMessage, 12, rxDelay, 0, 1);
            RxDelay = new Memory<byte>(rxDelay);
            // It's the configuration list, it can be empty or up to 15 bytes
            // - 17 = - 1 - 3 - 3 - 4 - 1 - 1 - 4
            // This is the size of all mandatory elements of the message
            var cfList = new byte[inputMessage.Length - 17];
            Array.Copy(inputMessage, 12, cfList, 0, inputMessage.Length - 17);
            Array.Reverse(cfList);
            CfList = new Memory<byte>(cfList);
            var mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            Mic = new Memory<byte>(mic);
        }

        public override byte[] PerformEncryption(string appSkey)
        {
            byte[] pt;
            if (!CfList.Span.IsEmpty)
            {
                pt = AppNonce.ToArray().Concat(NetID.ToArray()).Concat(DevAddr.ToArray()).Concat(DlSettings.ToArray()).Concat(RxDelay.ToArray()).Concat(CfList.ToArray()).Concat(Mic.ToArray()).ToArray();
            }
            else
            {
                pt = AppNonce.ToArray().Concat(NetID.ToArray()).Concat(DevAddr.ToArray()).Concat(DlSettings.ToArray()).Concat(RxDelay.ToArray()).Concat(Mic.ToArray()).ToArray();
            }

            using var aes = Aes.Create("AesManaged");
            aes.Key = ConversionHelper.StringToByteArray(appSkey);
            aes.IV = new byte[16];
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            ICryptoTransform cipher;

            cipher = aes.CreateDecryptor();
            var encryptedPayload = cipher.TransformFinalBlock(pt, 0, pt.Length);
            RawMessage = new byte[encryptedPayload.Length];
            Array.Copy(encryptedPayload, 0, RawMessage, 0, encryptedPayload.Length);
            return encryptedPayload;
        }

        public override byte[] GetByteMessage()
        {
            var messageArray = new List<byte>();
            messageArray.AddRange(Mhdr.ToArray());
            messageArray.AddRange(RawMessage);

            return messageArray.ToArray();
        }

        public override bool CheckMic(string nwskey, uint? server32BitFcnt = null)
        {
            throw new NotImplementedException();
        }

        public DownlinkPktFwdMessage Serialize(string appKey, string datr, Hertz freq, string devEui, long tmst, ushort lnsRxDelay = 0, uint? rfch = 0, string time = "", StationEui stationEui = default)
        {
            var algoinput = Mhdr.ToArray().Concat(AppNonce.ToArray()).Concat(NetID.ToArray()).Concat(DevAddr.ToArray()).Concat(DlSettings.ToArray()).Concat(RxDelay.ToArray()).ToArray();
            if (!CfList.Span.IsEmpty)
                algoinput = algoinput.Concat(CfList.ToArray()).ToArray();

            _ = CalculateMic(appKey, algoinput);
            _ = PerformEncryption(appKey);

            return new DownlinkPktFwdMessage(GetByteMessage(), datr, freq, devEui, tmst, lnsRxDelay, rfch, time, stationEui: stationEui);
        }
    }
}
