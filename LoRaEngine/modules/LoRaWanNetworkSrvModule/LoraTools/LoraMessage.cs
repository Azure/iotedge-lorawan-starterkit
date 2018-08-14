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

    public enum PhysicalIdentifier
    {
        PUSH_DATA,PUSH_ACK,PULL_DATA, PULL_RESP, PULL_ACK,TX_ACK
    }

    /// <summary>
    /// The Physical Payload wrapper
    /// </summary>
    public class PhysicalPayload
    {

        //case of inbound messages
        public PhysicalPayload(byte[] input)
        {

            protocolVersion = input[0];
            Array.Copy(input, 1, token, 0, 2);
            identifier = (PhysicalIdentifier)input[3];

            //PUSH_DATA That packet type is used by the gateway mainly to forward the RF packets received, and associated metadata, to the server
            if (identifier == PhysicalIdentifier.PUSH_DATA)
            {
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                message = new byte[input.Length - 12];
                Array.Copy(input, 12, message, 0, input.Length - 12);
            }

            //PULL_DATA That packet type is used by the gateway to poll data from the server.
            if (identifier == PhysicalIdentifier.PULL_DATA)
            {
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
            }

            //TX_ACK That packet type is used by the gateway to send a feedback to the to inform if a downlink request has been accepted or rejected by the gateway.
            if (identifier == PhysicalIdentifier.TX_ACK)
            {
                Console.WriteLine("TX ACK RECEIVED");
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                if (input.Length - 12 > 0)
                {
                    message = new byte[input.Length - 12];
                    Array.Copy(input, 12, message, 0, input.Length - 12);
                }
            }
        }

        //downlink transmission
        public PhysicalPayload(byte[] _token, PhysicalIdentifier type, byte[] _message)
        {
            //0x01 PUSH_ACK That packet type is used by the server to acknowledge immediately all the PUSH_DATA packets received.
            //0x04 PULL_ACK That packet type is used by the server to confirm that the network route is open and that the server can send PULL_RESP packets at any time.
            if (type == PhysicalIdentifier.PUSH_ACK || type == PhysicalIdentifier.PULL_ACK)
            {
                token = _token;
                identifier = type;
            }

            //0x03 PULL_RESP That packet type is used by the server to send RF packets and  metadata that will have to be emitted by the gateway.
            if (type == PhysicalIdentifier.PULL_RESP)
            {
                token = _token;
                identifier = type;
                message = new byte[_message.Length];
                Array.Copy(_message, 0, message, 0, _message.Length);
            
            }

        }

        //1 byte
        public byte protocolVersion = 2;
        //1-2 bytes
        public byte[] token =new byte[2];
        //1 byte
        public PhysicalIdentifier identifier;
        //8 bytes
        public byte[] gatewayIdentifier = new byte[8];
        //0-unlimited
        public byte[] message;

        public byte[] GetMessage()
        {
            List<byte> returnList = new List<byte>();
            returnList.Add(protocolVersion);
            returnList.AddRange(token);
            returnList.Add((byte)identifier);
            if(identifier==PhysicalIdentifier.PULL_DATA||
                identifier==PhysicalIdentifier.TX_ACK||
                identifier==PhysicalIdentifier.PUSH_DATA
                )
                returnList.AddRange(gatewayIdentifier);
            if(message !=null)
                returnList.AddRange(message);
            return returnList.ToArray();
        }
    }
    public class Txpk
    {
        public bool imme;
        public string data;
        public long tmst;
        public uint size;
        public double freq; //868
        public uint rfch;
        public string modu;
        public string datr;
        public string codr;
        public uint powe;
        public bool ipol;
    }

    public class Rxpk
    {
        public string time;
        public uint tmms;
        public uint tmst;
        public double freq; //868
        public uint chan;
        public uint rfch;
        public int stat;
        public string modu;
        public string datr;
        public string codr;
        public int rssi;
        public float lsnr;
        public uint size;
        public string data;
    }

    #region LoRaGenericPayload
    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common
    /// </summary>
    public abstract class LoRaGenericPayload
    {
        /// <summary>
        /// raw byte of the message
        /// </summary>
        public byte[] rawMessage;
        /// <summary>
        /// MACHeader of the message
        /// </summary>
        public byte[] mhdr;

        /// <summary>
        /// Message Integrity Code
        /// </summary>
        public byte[] mic;


        /// <summary>
        /// Assigned Dev Address
        /// </summary>
        public byte[] devAddr;


        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaGenericPayload(byte[] inputMessage)
        {
            rawMessage = inputMessage;
            //get the mhdr
            byte[] mhdr = new byte[1];
            Array.Copy(inputMessage, 0, mhdr, 0, 1);
            this.mhdr = mhdr;

            //MIC 4 last bytes
            byte[] mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            this.mic = mic;
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
        /// <returns></returns>
        public abstract string PerformEncryption(string appSkey);

        /// <summary>
        /// A Method to calculate the Mic of the message
        /// </summary>
        /// <param name="nwskey">The Network Secret Key</param>
        /// <returns></returns>
        public byte[] CalculateMic(string appKey,byte [] algoinput)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(appKey));
            mac.Init(key);
            byte[] rfu = new byte[1];
            rfu[0] = 0x0;
            //move


            byte[] msgLength = BitConverter.GetBytes(algoinput.Length);

            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            mic = result.Take(4).ToArray();
            return mic;
        }

        /// <summary>
        /// Method to take the different fields and assemble them in the message bytes
        /// </summary>
        /// <returns></returns>
        public abstract byte[] ToMessage();
        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public byte[] calculateKey(byte[] type,byte[] appnonce,byte[] netid, byte[] devnonce, byte[] appKey)
        {
            Aes aes = new AesManaged();
            aes.Key = appKey;
         
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            byte []pt = type.Concat(appnonce).Concat(netid).Concat(devnonce).Concat(new byte[7]).ToArray();

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

        //aka JoinEUI
        public byte[] appEUI;
        public byte[] devEUI;
        public byte[] devNonce;

        public LoRaPayloadJoinRequest(byte[] inputMessage) : base(inputMessage)
        {
            
            var inputmsgstr = BitConverter.ToString(inputMessage);
            //get the joinEUI field
            appEUI = new byte[8];
            Array.Copy(inputMessage, 1, appEUI, 0, 8);

            var appEUIStr = BitConverter.ToString(appEUI);
            //get the DevEUI
            devEUI = new byte[8];
            Array.Copy(inputMessage, 9, devEUI, 0, 8);

            var devEUIStr = BitConverter.ToString(devEUI);
            //get the DevNonce
            devNonce = new byte[2];
            Array.Copy(inputMessage, 17, devNonce, 0, 2);

            var devNonceStr = BitConverter.ToString(devNonce);

        }



        public override bool CheckMic(string AppKey)
        {
            //appEUI = StringToByteArray("526973696E674846");
            IMac mac = MacUtilities.GetMac("AESCMAC");

            KeyParameter key = new KeyParameter(StringToByteArray(AppKey));
            mac.Init(key);
            var appEUIStr = BitConverter.ToString(appEUI);
            var devEUIStr = BitConverter.ToString(devEUI);
            var devNonceStr = BitConverter.ToString(devNonce);

            var micstr = BitConverter.ToString(mic);

            var algoinput = mhdr.Concat(appEUI).Concat(devEUI).Concat(devNonce).ToArray();
            byte[] result = new byte[19];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            var resStr = BitConverter.ToString(result);
            return mic.SequenceEqual(result.Take(4).ToArray());
        }

        public override string PerformEncryption(string appSkey)
        {
            throw new NotImplementedException("The payload is not encrypted in case of a join message");
        }

        public override byte[] ToMessage()
        {
            throw new NotImplementedException();
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
        public byte[] fctrl;
        /// <summary>
        /// Frame Counter
        /// </summary>
        public byte[] fcnt;
        /// <summary>
        /// Optional frame
        /// </summary>
        public byte[] fopts;
        /// <summary>
        /// Port field
        /// </summary>
        public byte[] fport;
        /// <summary>
        /// MAC Frame Payload Encryption 
        /// </summary>
        public byte[] frmpayload;


        /// <summary>
        /// get message direction
        /// </summary>
        public int direction;


        /// <param name="inputMessage"></param>
        public LoRaPayloadStandardData(byte[] inputMessage) : base(inputMessage)
        {

            //get direction
            var checkDir = (mhdr[0] >> 5);
            //in this case the payload is not downlink of our type

     
            direction = (mhdr[0] & (1 << 6 - 1));

            //get the address
            byte[] addrbytes = new byte[4];
            Array.Copy(inputMessage, 1, addrbytes, 0, 4);
            //address correct but inversed
            Array.Reverse(addrbytes);
            this.devAddr = addrbytes;

            //Fctrl Frame Control Octet
            byte[] fctrl = new byte[1];
            Array.Copy(inputMessage, 5, fctrl, 0, 1);
            byte optlength = new byte();
            int foptsSize = (optlength << 4) >> 4;
            this.fctrl = fctrl;

            //Fcnt
            byte[] fcnt = new byte[2];
            Array.Copy(inputMessage, 6, fcnt, 0, 2);
            this.fcnt = fcnt;

            //FOpts
            byte[] fopts = new byte[foptsSize];
            Array.Copy(inputMessage, 8, fopts, 0, foptsSize);
            this.fopts = fopts;

            //Fport can be empty if no commands! 
            byte[] fport = new byte[1];
            Array.Copy(inputMessage, 8 + foptsSize, fport, 0, 1);
            this.fport = fport;

            //frmpayload
            byte[] FRMPayload = new byte[inputMessage.Length - 9 - 4 - foptsSize];
            Array.Copy(inputMessage, 9 + foptsSize, FRMPayload, 0, inputMessage.Length - 9 - 4 - foptsSize);
            this.frmpayload = FRMPayload;

        }

        public LoRaPayloadStandardData(byte[] _mhdr,byte[] _devAddr,byte[] _fctrl,byte[] _fcnt,byte [] _fOpts, byte [] _fPort ,byte [] _frmPayload,int _direction) : base()
        {
            mhdr = _mhdr;
            Array.Reverse(_devAddr);
            devAddr = _devAddr;
            fctrl = _fctrl;
            fcnt = _fcnt;
            fopts = _fOpts;
            fport = _fPort;
            frmpayload = _frmPayload;
            if(frmpayload!=null)
                Array.Reverse(frmpayload);
            direction=_direction;
        }

        /// <summary>
        /// Method to check if the mic is valid
        /// </summary>
        /// <param name="nwskey">the network security key</param>
        /// <returns></returns>
        public override bool CheckMic(string nwskey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block = { 0x49, 0x00, 0x00, 0x00, 0x00, (byte)direction, (byte)(devAddr[3]), (byte)(devAddr[2]), (byte)(devAddr[1]),
                (byte)(devAddr[0]),  fcnt[0] , fcnt[1],0x00, 0x00, 0x00, (byte)(rawMessage.Length-4) };
            var algoinput = block.Concat(rawMessage.Take(rawMessage.Length - 4)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return mic.SequenceEqual(result.Take(4).ToArray());
        }

        public  void SetMic(string nwskey)
        {
            rawMessage = this.ToMessage();
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block = { 0x49, 0x00, 0x00, 0x00, 0x00, (byte)direction, (byte)(devAddr[3]), (byte)(devAddr[2]), (byte)(devAddr[1]),
                (byte)(devAddr[0]),  fcnt[0] , fcnt[1],0x00, 0x00, 0x00, (byte)(rawMessage.Length) };
            var algoinput = block.Concat(rawMessage.Take(rawMessage.Length)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            mic = result.Take(4).ToArray();
        }

       

        /// <summary>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py
        /// </summary>
        public override string PerformEncryption(string appSkey)
        {
            if (frmpayload != null)
            {
                AesEngine aesEngine = new AesEngine();
                byte[] tmp = StringToByteArray(appSkey);

                aesEngine.Init(true, new KeyParameter(tmp));

                byte[] aBlock = { 0x01, 0x00, 0x00, 0x00, 0x00, (byte)direction, (byte)(devAddr[3]), (byte)(devAddr[2]), (byte)(devAddr[1]),
                (byte)(devAddr[0]),(byte)(fcnt[0]),(byte)(fcnt[1]),  0x00 , 0x00, 0x00, 0x00 };

                byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                int size = frmpayload.Length;
                byte[] decrypted = new byte[size];
                byte bufferIndex = 0;
                short ctr = 1;
                int i;
                while (size >= 16)
                {
                    aBlock[15] = (byte)((ctr) & 0xFF);
                    ctr++;
                    aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                    for (i = 0; i < 16; i++)
                    {
                        decrypted[bufferIndex + i] = (byte)(frmpayload[bufferIndex + i] ^ sBlock[i]);
                    }
                    size -= 16;
                    bufferIndex += 16;
                }
                if (size > 0)
                {
                    aBlock[15] = (byte)((ctr) & 0xFF);
                    aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                    for (i = 0; i < size; i++)
                    {
                        decrypted[bufferIndex + i] = (byte)(frmpayload[bufferIndex + i] ^ sBlock[i]);
                    }
                }
                frmpayload = decrypted;
                return Encoding.Default.GetString(decrypted);
            }else
                return null;
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
            List<byte> messageArray = new List<Byte>();
            messageArray.AddRange(mhdr);
            messageArray.AddRange(devAddr.Reverse().ToArray());
            messageArray.AddRange(fctrl);
            messageArray.AddRange(fcnt);
            if(fopts!=null)
                messageArray.AddRange(fopts);
            if(fport!=null)
            messageArray.AddRange(fport);
            if (frmpayload != null)
                messageArray.AddRange(frmpayload);
            if(mic!=null)
                messageArray.AddRange(mic);
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
        public byte[] appNonce;

        /// <summary>
        /// Device home network aka Home_NetId
        /// </summary>
        public byte[] netID;

        /// <summary>
        /// DLSettings
        /// </summary>
        public byte[] dlSettings;

        /// <summary>
        /// RxDelay
        /// </summary>
        public byte[] rxDelay;

        /// <summary>
        /// CFList / Optional
        /// </summary>
        public byte[] cfList;

        /// <summary>
        /// Frame Counter
        /// </summary>
        public byte[] fcnt;

        public LoRaPayloadJoinAccept(string _netId, string appKey, byte[] _devAddr, byte[] _appNonce)
        {
            appNonce = new byte[3];
            netID = new byte[3];
            devAddr = _devAddr;
            dlSettings = new byte[1] { 0};
            rxDelay = new byte[1] { 0 };
            //set payload Wrapper fields
            mhdr = new byte[] { 32};
            appNonce=_appNonce;
            netID = StringToByteArray(_netId.Replace("-",""));
            //default param 869.525 MHz / DR0 (F12, 125 kHz)  
      
            //TODO delete
            cfList = null;
           // cfList = StringToByteArray("184F84E85684B85E84886684586E8400");
            fcnt = BitConverter.GetBytes(0x01);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(appNonce);
                Array.Reverse(netID);
                Array.Reverse(devAddr);
            }
            var algoinput = mhdr.Concat(appNonce).Concat(netID).Concat(devAddr).Concat(dlSettings).Concat(rxDelay).ToArray();
            if (cfList != null)
                algoinput = algoinput.Concat(cfList).ToArray();

            CalculateMic(appKey,algoinput);
            PerformEncryption(appKey);
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public override string PerformEncryption(string appSkey)
        {
            //return null;
            AesEngine aesEngine = new AesEngine();
            var key = StringToByteArray(appSkey);
            aesEngine.Init(true, new KeyParameter(key));
            byte[] rfu = new byte[1];
            rfu[0] = 0x0;
          
            byte[] pt;
            if (cfList != null)
                pt = appNonce.Concat(netID).Concat(devAddr).Concat(rfu).Concat(rxDelay).Concat(cfList).Concat(mic).ToArray();
            else
                pt = appNonce.Concat(netID).Concat(devAddr).Concat(rfu).Concat(rxDelay).Concat(mic).ToArray();

            byte[] ct = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            Aes aes = new AesManaged();
            aes.Key = key;
            aes.IV = new byte[16];
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            ICryptoTransform cipher;    
          
                cipher = aes.CreateDecryptor();
            var encryptedPayload = cipher.TransformFinalBlock(pt, 0, pt.Length);
            rawMessage = new byte[encryptedPayload.Length];
            Array.Copy(encryptedPayload, 0, rawMessage, 0, encryptedPayload.Length);
            return Encoding.Default.GetString(encryptedPayload);
          
        }





        public override byte[] ToMessage()
        {
            List<byte> messageArray = new List<Byte>();
            messageArray.AddRange(mhdr);
            messageArray.AddRange(rawMessage);
    
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

        public PktFwdMessage fullPayload { get; set; }
        public string rawB64data { get; set; }
        public string decodedData { get; set; }



        /// <summary>
        /// Case of Uplink message. 
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(byte[] input)
        {  
            var payload = Encoding.Default.GetString(input);

            //todo ronnie implement a better logging by message type
            if (!payload.StartsWith("{\"stat"))
            Console.WriteLine(payload);


            var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
            fullPayload = payloadObject;
            //TODO to this in a loop.
            if (payloadObject.rxpk.Count > 0)
            {
                rawB64data = payloadObject.rxpk[0].data;
            }
       
        }

        /// <summary>
        /// Case of Downlink message. TODO refactor this
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(LoRaGenericPayload payloadMessage, LoRaMessageType messageType)
        {
            if(messageType == LoRaMessageType.JoinAccept)
             rawB64data = Convert.ToBase64String(((LoRaPayloadJoinAccept)payloadMessage).ToMessage());
            else if(messageType== LoRaMessageType.UnconfirmedDataDown||messageType==LoRaMessageType.ConfirmedDataDown)
                rawB64data = Convert.ToBase64String(((LoRaPayloadStandardData)payloadMessage).ToMessage());


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
        public bool isLoRaMessage = false;
        public LoRaGenericPayload payloadMessage;
        public LoRaMetada loraMetadata;
        public PhysicalPayload physicalPayload;

        /// <summary>
        /// The Message type
        /// </summary>
        public LoRaMessageType loRaMessageType;

        /// <summary>
        /// This contructor is used in case of uplink message, hence we don't know the message type yet
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaMessage(byte[] inputMessage)
        {
            //packet normally sent by the gateway as heartbeat. TODO find more elegant way to integrate.
         
            physicalPayload = new PhysicalPayload(inputMessage);
            if (physicalPayload.message != null)
            {
                loraMetadata = new LoRaMetada(physicalPayload.message);
                //set up the parts of the raw message   
                //status message
                if (loraMetadata.rawB64data != null)
                {
                    byte[] convertedInputMessage = Convert.FromBase64String(loraMetadata.rawB64data);
                    var messageType = convertedInputMessage[0] >> 5;
                    loRaMessageType = (LoRaMessageType)messageType;
                    //Uplink Message
                    if (messageType == (int)LoRaMessageType.UnconfirmedDataUp)
                        payloadMessage = new LoRaPayloadStandardData(convertedInputMessage);
                    else if (messageType == (int)LoRaMessageType.ConfirmedDataUp)
                        payloadMessage = new LoRaPayloadStandardData(convertedInputMessage);
                    else if (messageType == (int)LoRaMessageType.JoinRequest)
                        payloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
                    isLoRaMessage = true;
                }
                else
                {

                    isLoRaMessage = false;
                }
            }else
            {
                isLoRaMessage = false;
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
        public LoRaMessage(LoRaGenericPayload payload, LoRaMessageType type,byte[] physicalToken)
        {
            //construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                payloadMessage = (LoRaPayloadJoinAccept)payload;
                loraMetadata = new LoRaMetada(payloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(loraMetadata.rawB64data);
                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);
                
                physicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP,messageBytes);


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

        public LoRaMessage(LoRaGenericPayload payload, LoRaMessageType type, byte[] physicalToken,string _datr,uint _rfch,double _freq,long _tmst)
        {
            //construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                payloadMessage = (LoRaPayloadJoinAccept)payload;
                loraMetadata = new LoRaMetada(payloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(loraMetadata.rawB64data,_datr,_rfch,_freq, _tmst);
              
                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Console.WriteLine(jsonMsg);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                physicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);


            }
            else if (type == LoRaMessageType.UnconfirmedDataDown)
            {
                payloadMessage = (LoRaPayloadStandardData)payload;
                loraMetadata = new LoRaMetada(payloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(loraMetadata.rawB64data, _datr, _rfch, _freq, _tmst+ 1000000);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Console.WriteLine(jsonMsg);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                physicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }
            else if (type == LoRaMessageType.ConfirmedDataDown)
            {
                payloadMessage = (LoRaPayloadStandardData)payload;
                loraMetadata = new LoRaMetada(payloadMessage, type);
                var downlinkmsg = new DownlinkPktFwdMessage(loraMetadata.rawB64data, _datr, _rfch, _freq, _tmst + 1000000);

                var jsonMsg = JsonConvert.SerializeObject(downlinkmsg);
                Console.WriteLine(jsonMsg);
                var messageBytes = Encoding.Default.GetBytes(jsonMsg);

                physicalPayload = new PhysicalPayload(physicalToken, PhysicalIdentifier.PULL_RESP, messageBytes);
            }

        }

        /// <summary>
        /// Method to map the Mic check to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Neetwork Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public bool CheckMic(string nwskey)
        {
            return ((LoRaDataPayload)payloadMessage).CheckMic(nwskey);
        }

        /// <summary>
        /// Method to decrypt payload to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Application Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public string DecryptPayload(string appSkey)
        {
            var retValue = ((LoRaDataPayload)payloadMessage).PerformEncryption(appSkey);
            loraMetadata.decodedData = retValue;
            return retValue;
        }
    }
    #endregion

    #region PacketForwarder

    /// <summary>
    /// Base type of a Packet Forwarder message (lower level)
    /// </summary>
    public class PktFwdMessage
    {
        PktFwdType pktFwdType;
    }


    enum PktFwdType
    {
        Downlink,
        Uplink
    }

    /// <summary>
    /// JSON of a Downlink message for the Packet forwarder.
    /// </summary>
    public class DownlinkPktFwdMessage : PktFwdMessage
    {
        public Txpk txpk;


        //TODO change values to match network
        public DownlinkPktFwdMessage(string _data)
        {
            var byteData = Convert.FromBase64String(_data);
            txpk = new Txpk()
            {
                imme = true,
                data = _data,
                size = (uint)byteData.Length,
                freq = 869.525000,
                rfch = 0,
                modu = "LORA",
                datr = "SF12BW125",
                codr = "4/5",
                powe = 14

            };
        }

        public DownlinkPktFwdMessage(string _data,string _datr,uint _rfch,double _freq, long _tmst)
        {
            var byteData = Convert.FromBase64String(_data);
            txpk = new Txpk()
            {
                imme = false,
                tmst = _tmst,
                data = _data,
                size = (uint)byteData.Length,
                freq = _freq,
                rfch = _rfch,
                modu = "LORA",
                datr = _datr,
                codr = "4/5",
                powe = 14,
                ipol = true

            };
        }
    }


    /// <summary>
    /// an uplink Json for the packet forwarder.
    /// </summary>
    public class UplinkPktFwdMessage : PktFwdMessage
    {
        public List<Rxpk> rxpk = new List<Rxpk>();
    }


    #endregion
}
