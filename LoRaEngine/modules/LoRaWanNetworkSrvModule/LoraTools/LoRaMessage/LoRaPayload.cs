using LoRaTools.Utils;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LoRaTools.LoRaMessage
{
    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common
    /// </summary>
    public abstract class LoRaPayload
    {
        /// <summary>
        /// raw byte of the message
        /// </summary>
        public byte[] RawMessage { get; set; }

        /// <summary>
        /// MACHeader of the message
        /// </summary>
        public Memory<byte> Mhdr { get; set; }

        /// <summary>
        /// Message Integrity Code
        /// </summary>
        public Memory<byte> Mic { get; set; }

        /// <summary>
        /// Assigned Dev Address, TODO change??
        /// </summary>
        public byte[] DevAddr { get; set; }

        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaPayload(byte[] inputMessage)
        {
            RawMessage = inputMessage;       
            Mhdr = new Memory<byte>(RawMessage,0,1);

            // MIC 4 last bytes
            byte[] mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            this.Mic = new Memory<byte>(RawMessage, inputMessage.Length - 4,4);
        }

        /// <summary>
        /// This is used for downlink, The field will be computed at message creation
        /// </summary>
        public LoRaPayload()
        {
        }

        public LoRaMessageAdapter GetLoRaMessage()
        {
            LoRaMessageAdapter messageAdapter = new LoRaMessageAdapter(this);
            return messageAdapter;
        }

        /// <summary>
        /// Method to take the different fields and assemble them in the message bytes
        /// </summary>
        /// <returns>the message bytes</returns>
        public abstract byte[] GetByteMessage();

        /// <summary>
        /// Method to check a Mic
        /// </summary>
        /// <param name="nwskey">The Network Secret Key</param>
        /// <returns></returns>
        public abstract bool CheckMic(string nwskey);


        /// <summary>
        /// Method to calculate the encrypted version of the payload
        /// </summary>
        /// <param name="appSkey">the Application Secret Key</param>
        /// <returns>the encrypted bytes</returns>
        public abstract byte[] PerformEncryption(string appSkey);

        /// <summary>
        /// A Method to calculate the Mic of the message
        /// </summary>
        /// <param name="nwskey">The Network Secret Key</param>
        /// <returns> the Mic bytes</returns>
        public byte[] CalculateMic(string appKey, byte[] algoinput)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(appKey));
            mac.Init(key);
            byte[] rfu = new byte[1];
            rfu[0] = 0x0;


            byte[] msgLength = BitConverter.GetBytes(algoinput.Length);

            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            Mic = result.Take(4).ToArray();
            return Mic.ToArray();
        }

        public byte[] CalculateKey(byte[] type, byte[] appnonce, byte[] netid, byte[] devnonce, byte[] appKey)
        {
            Aes aes = new AesManaged();
            aes.Key = appKey;

            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            byte[] pt = type.Concat(appnonce).Concat(netid).Concat(devnonce).Concat(new byte[7]).ToArray();

            aes.IV = new byte[16];
            ICryptoTransform cipher;
            cipher = aes.CreateEncryptor();


            var key = cipher.TransformFinalBlock(pt, 0, pt.Length);


            return key;
        }

    }
}
