// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    public enum LoRaPayloadKeyType
    {
        NwkSkey = 1,
        AppSKey = 2
    }

    public enum LoRaMessageType : byte
    {
        // Request sent by device to join
        JoinRequest,

        // Response to a join request sent to device
        JoinAccept = 32,

        // Device to cloud message, no confirmation expected
        UnconfirmedDataUp = 64,

        // Cloud to device message, no confirmation expected
        UnconfirmedDataDown = 96,

        // Device to cloud message, confirmation required
        ConfirmedDataUp = 128,

        // Cloud to device message, confirmation required
        ConfirmedDataDown = 160
    }

    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common
    /// </summary>
    public abstract class LoRaPayload
    {
        public LoRaMessageType LoRaMessageType { get; set; }

        /// <summary>
        /// Gets or sets raw byte of the message
        /// </summary>
        public byte[] RawMessage { get; set; }

        /// <summary>
        /// Gets or sets mACHeader of the message
        /// </summary>
        public Memory<byte> Mhdr { get; set; }

        /// <summary>
        /// Gets or sets message Integrity Code
        /// </summary>
        public Memory<byte> Mic { get; set; }

        /// <summary>
        /// Gets or sets assigned Dev Address, TODO change??
        /// </summary>
        public Memory<byte> DevAddr { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        public LoRaPayload(byte[] inputMessage)
        {
            this.RawMessage = inputMessage;
            this.Mhdr = new Memory<byte>(this.RawMessage, 0, 1);
            // MIC 4 last bytes
            this.Mic = new Memory<byte>(this.RawMessage, inputMessage.Length - 4, 4);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// This is used for downlink, The field will be computed at message creation
        /// </summary>
        public LoRaPayload()
        {
        }

        /// <summary>
        /// Method to take the different fields and assemble them in the message bytes
        /// </summary>
        /// <returns>the message bytes</returns>
        [Obsolete("This method is planned to be deprecated in the next versions. Please use LoRaPayload instead.")]
        public abstract byte[] GetByteMessage();

        /// <summary>
        /// Method to check a Mic
        /// </summary>
        /// <param name="nwskey">The Network Secret Key</param>
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
            this.Mic = result.Take(4).ToArray();
            return this.Mic.ToArray();
        }

        /// <summary>
        /// Calculate the Netwok and Application Server Key used to encrypt data and compute MIC
        /// </summary>
        public byte[] CalculateKey(LoRaPayloadKeyType keyType, byte[] appnonce, byte[] netid, byte[] devnonce, byte[] appKey)
        {
            byte[] type = new byte[1];
            type[0] = (byte)keyType;
            Aes aes = new AesManaged
            {
                Key = appKey,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.None
            };

            byte[] pt = type.Concat(appnonce).Concat(netid).Concat(devnonce).Concat(new byte[7]).ToArray();

            aes.IV = new byte[16];
            ICryptoTransform cipher;
            cipher = aes.CreateEncryptor();
            var key = cipher.TransformFinalBlock(pt, 0, pt.Length);
            return key;
        }

        public static bool TryCreateLoRaPayload(Rxpk rxpk, out LoRaPayload loRaPayloadMessage)
        {
            byte[] convertedInputMessage = Convert.FromBase64String(rxpk.Data);
            var messageType = convertedInputMessage[0];

            switch (messageType)
            {
                case (int)LoRaMessageType.UnconfirmedDataUp:
                case (int)LoRaMessageType.ConfirmedDataUp:
                    loRaPayloadMessage = new LoRaPayloadData(convertedInputMessage);
                    break;

                case (int)LoRaMessageType.JoinRequest:
                    loRaPayloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
                    break;

                default:
                    loRaPayloadMessage = null;
                    return false;
            }

            loRaPayloadMessage.LoRaMessageType = (LoRaMessageType)messageType;
            return true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// Constructor used by the simulator
        /// </summary>
        public static bool TryCreateLoRaPayloadForSimulator(Txpk txpk, string appKey, out LoRaPayload loRaPayload)
        {
            if (txpk.Data != null)
            {
                byte[] convertedInputMessage = Convert.FromBase64String(txpk.Data);
                switch ((LoRaMessageType)convertedInputMessage[0])
                {
                    case LoRaMessageType.JoinRequest:
                        loRaPayload = new LoRaPayloadJoinRequest();
                        return true;
                    case LoRaMessageType.JoinAccept:
                        loRaPayload = new LoRaPayloadJoinAccept(convertedInputMessage, appKey);
                        return true;
                    case LoRaMessageType.UnconfirmedDataDown:
                    case LoRaMessageType.UnconfirmedDataUp:
                    case LoRaMessageType.ConfirmedDataUp:
                    case LoRaMessageType.ConfirmedDataDown:
                        loRaPayload = new LoRaPayloadData();
                        return true;
                }
            }

            loRaPayload = null;
            return false;
        }
    }
}
