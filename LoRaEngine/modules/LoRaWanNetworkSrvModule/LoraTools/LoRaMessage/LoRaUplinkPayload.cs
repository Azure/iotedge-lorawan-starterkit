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
    /// Common class for all the Uplink LoRa Messages.
    /// </summary>
    public abstract class LoRaDataPayload : LoRaGenericPayload
    {
        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaDataPayload(byte[] inputMessage) : base(inputMessage)
        {

        }

        /// <summary>
        /// This is used for downlink, when we need to compute those fields
        /// </summary>
        public LoRaDataPayload()
        {
        }



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
            KeyParameter key = new KeyParameter(StringToByteArray(appKey));
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


        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
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
