//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaTools;
using LoRaWan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PacketManager
{
    #region LoRaGenericPayload
    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common
    /// </summary>
    public abstract class LoRaGenericPayload
    {
        /// <summary>
        /// raw byte of the message
        /// </summary>
        public byte[] RawMessage { get; set; }

        /// <summary>
        /// MACHeader of the message
        /// </summary>
        public byte[] Mhdr { get; set; }

        /// <summary>
        /// Message Integrity Code
        /// </summary>
        public byte[] Mic { get; set; }

        /// <summary>
        /// Assigned Dev Address
        /// </summary>
        public byte[] DevAddr { get; set; }

        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaGenericPayload(byte[] inputMessage)
        {
            RawMessage = inputMessage;
            // get the mhdr
            byte[] mhdr = new byte[1];
            Array.Copy(inputMessage, 0, mhdr, 0, 1);
            this.Mhdr = mhdr;

            // MIC 4 last bytes
            byte[] mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            this.Mic = mic;
        }

        /// <summary>
        /// This is used for downlink, The field will be computed at message creation
        /// </summary>
        public LoRaGenericPayload()
        {
        }
    }
    #endregion

    #region LoRaUplinkPayload
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
            return Mic;
        }

        /// <summary>
        /// Method to take the different fields and assemble them in the message bytes
        /// </summary>
        /// <returns>the message bytes</returns>
        public abstract byte[] ToMessage();
        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type">Type 0x01 = NwkSKey, Type 0x02 = AppSKey</param>
        /// <param name="appnonce"></param>
        /// <param name="netid"></param>
        /// <param name="devnonce"></param>
        /// <param name="appKey"></param>
        /// <returns></returns>
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
    #endregion

    #region LoRaPayloadJoinRequest
    /// <summary>
    /// Implementation of the Join Request message type.
    /// </summary>
    public class LoRaPayloadJoinRequest : LoRaDataPayload
    {
        /// <summary>
        /// aka JoinEUI
        /// </summary>
        public byte[] AppEUI { get; set; }
        public byte[] DevEUI { get; set; }
        public byte[] DevNonce { get; set; }

        public LoRaPayloadJoinRequest(byte[] inputMessage) : base(inputMessage)
        {
            var inputmsgstr = BitConverter.ToString(inputMessage);
            // get the joinEUI field
            AppEUI = new byte[8];
            Array.Copy(inputMessage, 1, AppEUI, 0, 8);

            var appEUIStr = BitConverter.ToString(AppEUI);
            // get the DevEUI
            DevEUI = new byte[8];
            Array.Copy(inputMessage, 9, DevEUI, 0, 8);

            var devEUIStr = BitConverter.ToString(DevEUI);
            // get the DevNonce
            DevNonce = new byte[2];
            Array.Copy(inputMessage, 17, DevNonce, 0, 2);

            var devStr = BitConverter.ToString(DevNonce);

        }

        public LoRaPayloadJoinRequest(byte[] _AppEUI, byte[] _DevEUI, byte[] _DevNonce)
        {
            // Mhdr is always 0 in case of a join request
            Mhdr = new byte[1] { 0x00 };
            AppEUI = _AppEUI;
            DevEUI = _DevEUI;
            DevNonce = _DevNonce;
        }


        public override bool CheckMic(string appKey)
        {
            return Mic.SequenceEqual(PerformMic(appKey));
        }

        private byte[] PerformMic(string appKey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");

            KeyParameter key = new KeyParameter(StringToByteArray(appKey));
            mac.Init(key);
            var appEUIStr = BitConverter.ToString(AppEUI);
            var devEUIStr = BitConverter.ToString(DevEUI);
            var devNonceStr = BitConverter.ToString(DevNonce);

            // var micstr = BitConverter.ToString(Mic);

            var algoinput = Mhdr.Concat(AppEUI).Concat(DevEUI).Concat(DevNonce).ToArray();
            byte[] result = new byte[19];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            var resStr = BitConverter.ToString(result);
            return result.Take(4).ToArray();
        }

        public void SetMic(string appKey)
        {
            Mic = PerformMic(appKey);
        }
        public override byte[] PerformEncryption(string appSkey)
        {
            throw new NotImplementedException("The payload is not encrypted in case of a join message");
        }

        public override byte[] ToMessage()
        {
            List<byte> messageArray = new List<byte>();
            messageArray.AddRange(Mhdr);
            messageArray.AddRange(AppEUI);
            messageArray.AddRange(DevEUI);
            messageArray.AddRange(DevNonce);
            if (Mic != null)
            {
                messageArray.AddRange(Mic);
            }
            return messageArray.ToArray();
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
    #endregion
    #region LoRaPayloadUplink
    /// <summary>
    /// the body of an Uplink (normal) message
    /// </summary>
    public class LoRaPayloadStandardData : LoRaDataPayload
    {
        /// <summary>
        /// Frame control octet
        /// </summary>
        public byte[] Fctrl { get; set; }
        /// <summary>
        /// Frame Counter
        /// </summary>
        public byte[] Fcnt { get; set; }
        /// <summary>
        /// Optional frame
        /// </summary>
        public byte[] Fopts { get; set; }
        /// <summary>
        /// Port field
        /// </summary>
        public byte[] Fport { get; set; }
        /// <summary>
        /// MAC Frame Payload Encryption 
        /// </summary>
        public byte[] Frmpayload { get; set; }


        /// <summary>
        /// get message direction
        /// </summary>
        public int Direction { get; set; }

        public MacCommandHolder GetMacCommands()
        {
            Logger.Log("fopts : " + Fopts.Length, Logger.LoggingLevel.Full);
            MacCommandHolder macHolder = new MacCommandHolder(Fopts);
            return macHolder;
        }


        /// <param name="inputMessage"></param>
        public LoRaPayloadStandardData(byte[] inputMessage) : base(inputMessage)
        {
            // get direction
            var checkDir = Mhdr[0] >> 5;
            // in this case the payload is not downlink of our type


            Direction = Mhdr[0] & (1 << 6 - 1);

            // get the address
            byte[] addrbytes = new byte[4];
            Array.Copy(inputMessage, 1, addrbytes, 0, 4);
            // address correct but inversed
            Array.Reverse(addrbytes);
            this.DevAddr = addrbytes;

            // Fctrl Frame Control Octet
            byte[] fctrl = new byte[1];
            Array.Copy(inputMessage, 5, fctrl, 0, 1);
            int foptsSize = fctrl[0] & 0x0f;
            this.Fctrl = fctrl;

            // Fcnt
            byte[] fcnt = new byte[2];
            Array.Copy(inputMessage, 6, fcnt, 0, 2);
            this.Fcnt = fcnt;

            // FOpts
            byte[] fopts = new byte[foptsSize];
            Array.Copy(inputMessage, 8, fopts, 0, foptsSize);
            this.Fopts = fopts;

            // Fport can be empty if no commands! 
            byte[] fport = new byte[1];
            Array.Copy(inputMessage, 8 + foptsSize, fport, 0, 1);
            this.Fport = fport;

            // frmpayload
            byte[] fRMPayload = new byte[inputMessage.Length - 9 - 4 - foptsSize];
            Array.Copy(inputMessage, 9 + foptsSize, fRMPayload, 0, inputMessage.Length - 9 - 4 - foptsSize);
            this.Frmpayload = fRMPayload;

        }

        public LoRaPayloadStandardData(byte[] _mhdr, byte[] _devAddr, byte[] _fctrl, byte[] _fcnt, byte[] _fOpts, byte[] _fPort, byte[] _frmPayload, int _direction) : base()
        {
            Mhdr = _mhdr;
            Array.Reverse(_devAddr);
            DevAddr = _devAddr;
            if (_fOpts != null)
            {
                _fctrl[0] = BitConverter.GetBytes((int)_fctrl[0] + _fOpts.Length)[0];
            }
            Fctrl = _fctrl;
            Fcnt = _fcnt;
            Fopts = _fOpts;
            Fport = _fPort;
            Frmpayload = _frmPayload;
            if (Frmpayload != null)
                Array.Reverse(Frmpayload);
            Direction = _direction;
        }

        /// <summary>
        /// Method to check if the mic is valid
        /// </summary>
        /// <param name="nwskey">the network security key</param>
        /// <returns>if the Mic is valid or not</returns>
        public override bool CheckMic(string nwskey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block =
                {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr[3], (byte)DevAddr[2], (byte)DevAddr[1],
                (byte)DevAddr[0], Fcnt[0], Fcnt[1], 0x00, 0x00, 0x00, (byte)(RawMessage.Length - 4)
            };
            var algoinput = block.Concat(RawMessage.Take(RawMessage.Length - 4)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return Mic.SequenceEqual(result.Take(4).ToArray());
        }

        public void SetMic(string nwskey)
        {
            RawMessage = this.ToMessage();
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block =
                {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr[3], (byte)DevAddr[2], (byte)DevAddr[1],
                (byte)DevAddr[0], Fcnt[0], Fcnt[1], 0x00, 0x00, 0x00, (byte)RawMessage.Length
            };
            var algoinput = block.Concat(RawMessage.Take(RawMessage.Length)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            Mic = result.Take(4).ToArray();
        }



        /// <summary>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py
        /// </summary>
        public override byte[] PerformEncryption(string appSkey)
        {
            if (Frmpayload != null)
            {
                AesEngine aesEngine = new AesEngine();
                byte[] tmp = StringToByteArray(appSkey);

                aesEngine.Init(true, new KeyParameter(tmp));

                byte[] aBlock =
                    {
                    0x01, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr[3], (byte)DevAddr[2], (byte)DevAddr[1],
                (byte)DevAddr[0], (byte)Fcnt[0], (byte)Fcnt[1], 0x00, 0x00, 0x00, 0x00
                };

                byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                int size = Frmpayload.Length;
                byte[] decrypted = new byte[size];
                byte bufferIndex = 0;
                short ctr = 1;
                int i;
                while (size >= 16)
                {
                    aBlock[15] = (byte)(ctr & 0xFF);
                    ctr++;
                    aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                    for (i = 0; i < 16; i++)
                    {
                        decrypted[bufferIndex + i] = (byte)(Frmpayload[bufferIndex + i] ^ sBlock[i]);
                    }
                    size -= 16;
                    bufferIndex += 16;
                }
                if (size > 0)
                {
                    aBlock[15] = (byte)(ctr & 0xFF);
                    aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                    for (i = 0; i < size; i++)
                    {
                        decrypted[bufferIndex + i] = (byte)(Frmpayload[bufferIndex + i] ^ sBlock[i]);
                    }
                }
                Frmpayload = decrypted;
                return decrypted;
            }
            else
            {
                return null;
            }
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public override byte[] ToMessage()
        {
            List<byte> messageArray = new List<byte>();
            messageArray.AddRange(Mhdr);
            messageArray.AddRange(DevAddr.Reverse().ToArray());
            messageArray.AddRange(Fctrl);
            messageArray.AddRange(Fcnt);
            if (Fopts != null)
            {
                messageArray.AddRange(Fopts);
            }
            if (Fport != null)
            {
                messageArray.AddRange(Fport);
            }
            if (Frmpayload != null)
            {
                messageArray.AddRange(Frmpayload);
            }
            if (Mic != null)
            {
                messageArray.AddRange(Mic);
            }
            return messageArray.ToArray();
        }


    }
    #endregion

    #region LoRaPayloadJoinAccept
    /// <summary>
    /// Implementation of a LoRa Join-Accept frame
    /// </summary>
    public class LoRaPayloadJoinAccept : LoRaDataPayload
    {
        /// <summary>
        /// Server Nonce aka JoinNonce
        /// </summary>
        public byte[] AppNonce { get; set; }

        /// <summary>
        /// Device home network aka Home_NetId
        /// </summary>
        public byte[] NetID { get; set; }

        /// <summary>
        /// DLSettings
        /// </summary>
        public byte[] DlSettings { get; set; }

        /// <summary>
        /// RxDelay
        /// </summary>
        public byte[] RxDelay { get; set; }

        /// <summary>
        /// CFList / Optional
        /// </summary>
        public byte[] CfList { get; set; }

        /// <summary>
        /// Frame Counter
        /// </summary>
        public byte[] Fcnt { get; set; }

        public LoRaPayloadJoinAccept(byte[] inputMessage, string appKey)
        {
            // Only MHDR is not encrypted with the key
            // ( PHYPayload = MHDR[1] | MACPayload[..] | MIC[4] )
            Mhdr = new byte[1];
            Array.Copy(inputMessage, 0, Mhdr, 0, 1);
            // Then we will take the rest and decrypt it
            AesEngine aesEngine = new AesEngine();
            var key = StringToByteArray(appKey);
            aesEngine.Init(true, new KeyParameter(key));
            Aes aes = new AesManaged();
            aes.Key = key;
            aes.IV = new byte[16];
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            ICryptoTransform cipher;

            cipher = aes.CreateDecryptor();
            byte[] pt = new byte[inputMessage.Length - 1];
            Array.Copy(inputMessage, 1, pt, 0, pt.Length);
            //Array.Reverse(pt);
            var decryptedPayload = cipher.TransformFinalBlock(pt, 0, pt.Length);
            // We will copy back in the main inputMessage the content
            Array.Copy(decryptedPayload, 0, inputMessage, 1, decryptedPayload.Length);

            // ( MACPayload = AppNonce[3] | NetID[3] | DevAddr[4] | DLSettings[1] | RxDelay[1] | CFList[0|15] )
            AppNonce = new byte[3];
            Array.Copy(inputMessage, 1, AppNonce, 0, 3);
            Array.Reverse(AppNonce);
            NetID = new byte[3];
            Array.Copy(inputMessage, 4, NetID, 0, 3);
            Array.Reverse(NetID);
            DevAddr = new byte[4];
            Array.Copy(inputMessage, 7, DevAddr, 0, 4);
            Array.Reverse(DevAddr);
            DlSettings = new byte[1];
            Array.Copy(inputMessage, 11, DlSettings, 0, 1);
            RxDelay = new byte[1];
            Array.Copy(inputMessage, 11, RxDelay, 0, 1);
            // It's the configuration list, it can be empty or up to 15 bytes
            // - 17 = - 1 - 3 - 3 - 4 - 1 - 1 - 4
            // This is the size of all mandatory elements of the message
            CfList = new byte[inputMessage.Length - 17];
            Array.Copy(inputMessage, 12, CfList, 0, inputMessage.Length - 17);
            Array.Reverse(CfList);
            Mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, Mic, 0, 4);

        }

        public LoRaPayloadJoinAccept(string _netId, string appKey, byte[] _devAddr, byte[] _appNonce)
        {
            AppNonce = new byte[3];
            NetID = new byte[3];
            DevAddr = _devAddr;
            DlSettings = new byte[1] { 0 };
            RxDelay = new byte[1] { 0 };
            // set payload Wrapper fields
            Mhdr = new byte[] { 32 };
            AppNonce = _appNonce;
            NetID = StringToByteArray(_netId.Replace("-", string.Empty));
            // default param 869.525 MHz / DR0 (F12, 125 kHz)  
            CfList = null;
            // cfList = StringToByteArray("184F84E85684B85E84886684586E8400");
            Fcnt = BitConverter.GetBytes(0x01);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(AppNonce);
                Array.Reverse(NetID);
                Array.Reverse(DevAddr);
            }
            var algoinput = Mhdr.Concat(AppNonce).Concat(NetID).Concat(DevAddr).Concat(DlSettings).Concat(RxDelay).ToArray();
            if (CfList != null)
                algoinput = algoinput.Concat(CfList).ToArray();

            CalculateMic(appKey, algoinput);
            PerformEncryption(appKey);
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public override byte[] PerformEncryption(string appSkey)
        {
            AesEngine aesEngine = new AesEngine();
            var key = StringToByteArray(appSkey);
            aesEngine.Init(true, new KeyParameter(key));
            byte[] rfu = new byte[1];
            rfu[0] = 0x0;

            byte[] pt;
            if (CfList != null)
            {
                pt = AppNonce.Concat(NetID).Concat(DevAddr).Concat(rfu).Concat(RxDelay).Concat(CfList).Concat(Mic).ToArray();
            }
            else
            {
                pt = AppNonce.Concat(NetID).Concat(DevAddr).Concat(rfu).Concat(RxDelay).Concat(Mic).ToArray();
            }
            // byte[] ct = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            Aes aes = new AesManaged();
            aes.Key = key;
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

        public override byte[] ToMessage()
        {
            List<byte> messageArray = new List<byte>();
            messageArray.AddRange(Mhdr);
            messageArray.AddRange(RawMessage);

            return messageArray.ToArray();
        }

        public override bool CheckMic(string nwskey)
        {
            throw new NotImplementedException();
        }


    }
    #endregion

    #region LoRaMetada

    /// <summary>
    /// Metadata about a Lora Packet, featuring a Lora Packet, the payload and the data.
    /// </summary>
    public class LoRaMetada
    {
        public PktFwdMessage FullPayload { get; set; }
        public Txpk Txpk { get; set; }
        public string RawB64data { get; set; }
        public byte[] DecodedData { get; set; }



        /// <summary>
        /// Case of Uplink message. 
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(byte[] input)
        {
            var payload = Encoding.Default.GetString(input);

            // todo ronnie implement a better logging by message type
            if (!payload.StartsWith("{\"stat"))
                Logger.Log($"DataUp {payload}", Logger.LoggingLevel.Full);


            var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
            FullPayload = payloadObject;
            // TODO to this in a loop.
            if (payloadObject.rxpk.Count > 0)
            {
                RawB64data = payloadObject.rxpk[0].data;
            }
            else
            {
                //if there is no rxpk, then maybe there is txpk
                try
                {
                    var txpkObject = JsonConvert.DeserializeObject<DownlinkPktFwdMessage>(payload);
                    if (txpkObject != null)
                        Txpk = txpkObject.txpk;
                    RawB64data = txpkObject.txpk.data;
                }
                catch (Exception)
                {

                    Logger.Log($"Not an uplink message and not a downlink message", Logger.LoggingLevel.Full);
                }

            }
        }

        /// <summary>
        /// Case of Downlink message. TODO refactor this
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(LoRaGenericPayload payloadMessage, LoRaMessageType messageType)
        {
            if (messageType == LoRaMessageType.JoinAccept)
            {
                RawB64data = Convert.ToBase64String(((LoRaPayloadJoinAccept)payloadMessage).ToMessage());
            }
            else if (messageType == LoRaMessageType.UnconfirmedDataDown || messageType == LoRaMessageType.ConfirmedDataDown)
            {
                RawB64data = Convert.ToBase64String(((LoRaPayloadStandardData)payloadMessage).ToMessage());
            }
        }
    }
    #endregion

    #region LoRaMessage
    public enum LoRaMessageType
    {
        JoinRequest,
        JoinAccept,
        UnconfirmedDataUp,
        UnconfirmedDataDown,
        ConfirmedDataUp,
        ConfirmedDataDown,
        RFU,
        Proprietary
    }
    /// <summary>
    /// class exposing usefull message stuff
    /// </summary>
    public class LoRaMessage
    {
        /// <summary>
        /// see 
        /// </summary>
        public bool IsLoRaMessage = false;
        public LoRaGenericPayload PayloadMessage { get; set; }
        public LoRaMetada LoraMetadata { get; set; }
        public PhysicalPayload PhysicalPayload { get; set; }

        /// <summary>
        /// The Message type
        /// </summary>
        public LoRaMessageType LoRaMessageType { get; set; }

        /// <summary>
        /// This contructor is used in case of uplink message, hence we don't know the message type yet
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaMessage(byte[] inputMessage, bool server = false, string AppKey = "")
        {
            // packet normally sent by the gateway as heartbeat. TODO find more elegant way to integrate.

            PhysicalPayload = new PhysicalPayload(inputMessage, server);
            if (PhysicalPayload.message != null)
            {
                LoraMetadata = new LoRaMetada(PhysicalPayload.message);
                // set up the parts of the raw message   
                // status message
                if (LoraMetadata.RawB64data != null)
                {
                    byte[] convertedInputMessage = Convert.FromBase64String(LoraMetadata.RawB64data);
                    var messageType = convertedInputMessage[0] >> 5;
                    LoRaMessageType = (LoRaMessageType)messageType;
                    // Uplink Message
                    if (messageType == (int)LoRaMessageType.UnconfirmedDataUp)
                    {
                        PayloadMessage = new LoRaPayloadStandardData(convertedInputMessage);
                    }
                    else if (messageType == (int)LoRaMessageType.ConfirmedDataUp)
                    {
                        PayloadMessage = new LoRaPayloadStandardData(convertedInputMessage);
                    }
                    else if (messageType == (int)LoRaMessageType.JoinRequest)
                    {
                        PayloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
                    }
                    else if (messageType == (int)LoRaMessageType.JoinAccept)
                    {
                        PayloadMessage = new LoRaPayloadJoinAccept(convertedInputMessage, AppKey);
                    }
                    IsLoRaMessage = true;
                }
                else
                {
                    IsLoRaMessage = false;
                }
            }
            else
            {
                IsLoRaMessage = false;
            }
        }

        /// <summary>
        /// This contructor is used in case of downlink message
        /// </summary>
        /// <param name="inputMessage"></param>
        /// <param name="type">
        /// 0 = Join Request
        /// 1 = Join Accept
        /// 2 = Unconfirmed Data up
        /// 3 = Unconfirmed Data down
        /// 4 = Confirmed Data up
        /// 5 = Confirmed Data down
        /// 6 = Rejoin Request</param>
        public LoRaMessage(LoRaGenericPayload payload, LoRaMessageType type, byte[] physicalToken)
        {
            // construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                PayloadMessage = (LoRaPayloadJoinAccept)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data);
                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);
                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
            else if (type == LoRaMessageType.UnconfirmedDataDown)
            {
                throw new NotImplementedException();
            }
            else if (type == LoRaMessageType.ConfirmedDataDown)
            {
                throw new NotImplementedException();
            }
        }

        public LoRaMessage(LoRaGenericPayload payload, LoRaMessageType type, byte[] physicalToken, string _datr, uint _rfch, double _freq, long _tmst)
        {
            // construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                PayloadMessage = (LoRaPayloadJoinAccept)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data, _datr, _rfch, _freq, _tmst);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Logger.Log($"JoinAccept {jsonMsg}", Logger.LoggingLevel.Full);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
            else if (type == LoRaMessageType.UnconfirmedDataDown)
            {
                PayloadMessage = (LoRaPayloadStandardData)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data, _datr, _rfch, _freq, _tmst + 1000000);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Logger.Log($"UnconfirmedDataDown {jsonMsg}", Logger.LoggingLevel.Full);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
            else if (type == LoRaMessageType.ConfirmedDataDown)
            {
                PayloadMessage = (LoRaPayloadStandardData)payload;
                LoraMetadata = new LoRaMetada(PayloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(LoraMetadata.RawB64data, _datr, _rfch, _freq, _tmst + 1000000);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Logger.Log($"ConfirmedDataDown {jsonMsg}", Logger.LoggingLevel.Full);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                PhysicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
        }

        /// <summary>
        /// Method to map the Mic check to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Neetwork Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public bool CheckMic(string nwskey)
        {
            return ((LoRaDataPayload)PayloadMessage).CheckMic(nwskey);
        }

        /// <summary>
        /// Method to decrypt payload to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Application Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public byte[] DecryptPayload(string appSkey)
        {
            var retValue = ((LoRaDataPayload)PayloadMessage).PerformEncryption(appSkey);
            LoraMetadata.DecodedData = retValue;
            return retValue;
        }
    }
    #endregion


}
