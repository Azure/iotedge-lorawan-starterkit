using LoRaTools.LoRaPhysical;
using LoRaTools.Utils;
using LoRaWan;
using Newtonsoft.Json;
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

        public LoRaPayloadJoinAccept(string netId, byte[] devAddr, byte[] appNonce, byte[] dlSettings, byte[] rxDelay, byte[] cfList)
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

           
        }
        [Obsolete("To be discontinued as part of messageProcessor refactor")]
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

        public LoRaPayloadJoinAccept(byte[] inputMessage, string appKey)
        {
            // Only MHDR is not encrypted with the key
            // ( PHYPayload = MHDR[1] | MACPayload[..] | MIC[4] )
            Mhdr = new Memory<byte>(inputMessage, 0, 1);
            // Then we will take the rest and decrypt it
            //DecryptPayload(inputMessage);
            //var decrypted = PerformEncryption(appKey);
            //Array.Copy(decrypted, 0, inputMessage, 0, decrypted.Length);
            //DecryptPayload(inputMessage);
            AesEngine aesEngine = new AesEngine();
            var key = ConversionHelper.StringToByteArray(appKey);
            aesEngine.Init(true, new KeyParameter(key));
            Aes aes = new AesManaged();
            aes.Key = key;
            aes.IV = new byte[16];
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            ICryptoTransform cipher;

            cipher = aes.CreateEncryptor();
            byte[] pt = new byte[inputMessage.Length - 1];
            Array.Copy(inputMessage, 1, pt, 0, pt.Length);
            //Array.Reverse(pt);
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
            Array.Copy(inputMessage, 11, rxDelay, 0, 1);
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

        public override DownlinkPktFwdMessage Serialize( string appKey, string nwkKey,string datr, double freq, long tmst, string devEUI)
        {
            var algoinput = Mhdr.ToArray().Concat(AppNonce.ToArray()).Concat(NetID.ToArray()).Concat(DevAddr.ToArray()).Concat(DlSettings.ToArray()).Concat(RxDelay.ToArray()).ToArray();
            if (!CfList.Span.IsEmpty)
                algoinput = algoinput.Concat(CfList.ToArray()).ToArray();

            CalculateMic(appKey, algoinput);
            PerformEncryption(appKey);

            var downlinkPktFwdMessage = new DownlinkPktFwdMessage(this.GetByteMessage(), datr, freq, tmst);
            if (Logger.LoggerLevel < Logger.LoggingLevel.Info)
            {
                var jsonMsg = JsonConvert.SerializeObject(downlinkPktFwdMessage);

                if (devEUI.Length != 0)
                {
                    Logger.Log(devEUI, $"{((LoRaMessageType)(Mhdr.Span[0])).ToString()} {jsonMsg}", Logger.LoggingLevel.Full);
                }
                else
                {
                    Logger.Log(ConversionHelper.ByteArrayToString(this.DevAddr.Span.ToArray()), $"{((LoRaMessageType)(Mhdr.Span[0])).ToString()} {jsonMsg}", Logger.LoggingLevel.Full);
                }
            }

            return downlinkPktFwdMessage;
        }
    }
    }
