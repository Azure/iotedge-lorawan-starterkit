using LoRaTools.LoRaPhysical;
using LoRaTools.Utils;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace LoRaTools.LoRaMessage
{
    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common
    /// </summary>
    public abstract class LoRaPayload
    {
        /// <summary>
        /// Used when calculating the Network and App SKey
        /// </summary>
        public enum KeyType
        {
            NwkSKey = 1,
            AppSKey = 2,
        }

        public LoRaMessageType LoRaMessageType { get; set; }

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
        public Memory<byte> DevAddr { get; set; }

        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaPayload(byte[] inputMessage)
        {
            RawMessage = inputMessage;
            Mhdr = new Memory<byte>(RawMessage, 0, 1);
            // MIC 4 last bytes
            this.Mic = new Memory<byte>(RawMessage, inputMessage.Length - 4, 4);
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

        /// <summary>
        /// Calculate the Netwok and Application Server Key used to encrypt data and compute MIC
        /// </summary>
        /// <param name="keyType">0x01 = NwkSKey, 0x02 = AppSKey</param>
        /// <param name="appnonce"></param>
        /// <param name="netid"></param>
        /// <param name="devnonce"></param>
        /// <param name="appKey"></param>
        /// <returns></returns>
        public byte[] CalculateKey(KeyType keyType, byte[] appnonce, byte[] netid, byte[] devnonce, byte[] appKey)
        {
            byte[] type = new byte[1];
            type[0] = (byte)keyType;
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

        public static bool TryCreateLoRaPayload(Rxpk rxpk, out LoRaPayload loRaPayloadMessage)
        {

            byte[] convertedInputMessage = Convert.FromBase64String(rxpk.data);
            var messageType = convertedInputMessage[0];
            //LoRaMessageType = (LoRaMessageType)messageType;


            if (messageType == (int)LoRaMessageType.UnconfirmedDataUp)
            {
                loRaPayloadMessage = new LoRaPayloadData(convertedInputMessage);

            }
            else if (messageType == (int)LoRaMessageType.ConfirmedDataUp)
            {
                loRaPayloadMessage = new LoRaPayloadData(convertedInputMessage);
            }
            else if (messageType == (int)LoRaMessageType.JoinRequest)
            {
                loRaPayloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
            }
            else
            {
                loRaPayloadMessage = null;
                return false;
            }

            loRaPayloadMessage.LoRaMessageType = (LoRaMessageType)messageType;
            return true;
        }

    }
}

    

