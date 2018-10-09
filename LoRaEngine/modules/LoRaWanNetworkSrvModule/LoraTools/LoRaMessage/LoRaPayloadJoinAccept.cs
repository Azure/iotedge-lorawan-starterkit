using LoRaTools.Utils;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LoRaTools.LoRaMessage
{
    /// <summary>
    /// Implementation of a LoRa Join-Accept frame
    /// </summary>
    public class LoRaPayloadJoinAccept : LoRaPayload
    {
        /// <summary>
        /// Server Nonce aka JoinNonce
        /// </summary>
        public Memory<byte> AppNonce { get; set; }

        /// <summary>
        /// Device home network aka Home_NetId
        /// </summary>
        public Memory<byte> NetID { get; set; }

        /// <summary>
        /// DLSettings
        /// </summary>
        public Memory<byte> DlSettings { get; set; }

        /// <summary>
        /// RxDelay
        /// </summary>
        public Memory<byte> RxDelay { get; set; }

        /// <summary>
        /// CFList / Optional
        /// </summary>
        public Memory<byte> CfList { get; set; }

        /// <summary>
        /// Frame Counter
        /// </summary>
        public Memory<byte> Fcnt { get; set; }

        public LoRaPayloadJoinAccept(string netId, string appKey, byte[] devAddr, byte[] appNonce, byte[] dlSettings, byte[] rxDelay, byte[] cfList)
        {
            int cfListLength = cfList == null ? 0 : cfList.Length;
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
            }
            var algoinput = Mhdr.ToArray().Concat(AppNonce.ToArray()).Concat(NetID.ToArray()).Concat(DevAddr.ToArray()).Concat(DlSettings.ToArray()).Concat(RxDelay.ToArray()).ToArray();
            if (!CfList.Span.IsEmpty)
                algoinput = algoinput.Concat(CfList.ToArray()).ToArray();

            CalculateMic(appKey, algoinput);
            PerformEncryption(appKey);
        }


        public override byte[] PerformEncryption(string appSkey)
        {
            byte[] rfu = new byte[1];
            rfu[0] = 0x0;

            byte[] pt;
            if (!CfList.Span.IsEmpty)
            {
                pt = AppNonce.ToArray().Concat(NetID.ToArray()).Concat(DevAddr.ToArray()).Concat(rfu).Concat(RxDelay.ToArray()).Concat(CfList.ToArray()).Concat(Mic.ToArray()).ToArray();
            }
            else
            {
                pt = AppNonce.ToArray().Concat(NetID.ToArray()).Concat(DevAddr.ToArray()).Concat(rfu).Concat(RxDelay.ToArray()).Concat(Mic.ToArray()).ToArray();
            }

            Aes aes = new AesManaged();
            aes.Key = ConversionHelper.StringToByteArray(appSkey);
            aes.IV = new byte[16];
            aes.Mode = CipherMode.ECB;
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
            List<byte> messageArray = new List<byte>();
            messageArray.AddRange(Mhdr.ToArray());
            messageArray.AddRange(RawMessage);

            return messageArray.ToArray();
        }

        public override bool CheckMic(string nwskey)
        {
            throw new NotImplementedException();
        }


    }
}
