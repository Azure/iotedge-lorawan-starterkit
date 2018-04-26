using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Linq;
using System.Text;

namespace PacketManager
{
    public class LoRaPayloadMessage
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
        /// Device MAC Address
        /// </summary>
        public byte[] devAddr;
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
        /// Message Integrity Code
        /// </summary>
        public byte[] mic;

        /// <summary>
        /// get message direction
        /// </summary>
        public int direction;
        public bool processed;


        /// <param name="inputMessage"></param>
        public LoRaPayloadMessage(byte[] inputMessage)
        {
            rawMessage = inputMessage;
            //get the mhdr
            byte[] mhdr = new byte[1];
            Array.Copy(inputMessage, 0, mhdr, 0, 1);
            this.mhdr = mhdr;
            //get direction
            var checkDir=(mhdr[0] >> 5);
            //in this case the payload is not downlink of our type

            if (checkDir != 2)
            {
                processed = false;
                return;
            }
            processed = true;
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

            //MIC 4 last bytes
            byte[] mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            this.mic = mic;
        }
    }


    public class LoRaMetada
    {
        public byte[] gatewayMacAddress { get; set; }
      public dynamic fullPayload { get; set; }
        public string rawB64data { get; set; }
        public string devAddr { get; set; }
        public string decodedData { get; set; }
        public bool processed { get; set; }

        public LoRaMetada(byte[] input)
        {
            gatewayMacAddress= input.Skip(4).Take(6).ToArray();
            var c = BitConverter.ToString(gatewayMacAddress);
            var payload =Encoding.Default.GetString(input.Skip(12).ToArray());
            if (payload.Count()==0)
            {
                processed = false;
                return;
            }
            else
                processed = true;
            
            fullPayload= JObject.Parse(payload);
            rawB64data = Convert.ToString(fullPayload.rxpk[0].data);


            //get the address
            byte[] addrbytes = new byte[4];
            Array.Copy(input, 1, addrbytes, 0, 4);
            //address correct but inversed
            Array.Reverse(addrbytes);
            devAddr = BitConverter.ToString(addrbytes);
        }
    }
   
    /// <summary>
    /// class exposing usefull message stuff
    /// </summary>
    public class LoRaMessage
    {
        public LoRaPayloadMessage payloadMessage;
        public LoRaMetada lorametadata;

        public LoRaMessage(byte[] inputMessage)
        {
            lorametadata = new LoRaMetada(inputMessage);
            //set up the parts of the raw message 
            if(lorametadata.processed)
                payloadMessage = new LoRaPayloadMessage(Convert.FromBase64String(lorametadata.rawB64data));

        }

        /// <summary>
        /// Method to check if the mic is valid
        /// </summary>
        /// <param name="nwskey">the network security key</param>
        /// <returns></returns>
        public bool CheckMic(string nwskey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block = { 0x49, 0x00, 0x00, 0x00, 0x00, (byte)this.payloadMessage.direction, (byte)(this.payloadMessage.devAddr[3]), (byte)(payloadMessage.devAddr[2]), (byte)(payloadMessage.devAddr[1]),
                (byte)(payloadMessage.devAddr[0]),  this.payloadMessage.fcnt[0] , this.payloadMessage.fcnt[1],0x00, 0x00, 0x00, (byte)(this.payloadMessage.rawMessage.Length-4) };
            var algoinput = block.Concat(this.payloadMessage.rawMessage.Take(this.payloadMessage.rawMessage.Length - 4)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return this.payloadMessage.mic.SequenceEqual(result.Take(4).ToArray());
        }

        /// <summary>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py
        /// </summary>
        public byte[] DecryptPayload(string appSkey)
        {
            AesEngine aesEngine = new AesEngine();
            aesEngine.Init(true, new KeyParameter(StringToByteArray(appSkey)));

            byte[] aBlock = { 0x01, 0x00, 0x00, 0x00, 0x00, (byte)this.payloadMessage.direction, (byte)(payloadMessage.devAddr[3]), (byte)(payloadMessage.devAddr[2]), (byte)(payloadMessage.devAddr[1]),
                (byte)(payloadMessage.devAddr[0]),(byte)(payloadMessage.fcnt[0]),(byte)(payloadMessage.fcnt[1]),  0x00 , 0x00, 0x00, 0x00 };

            byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            int size = payloadMessage.frmpayload.Length;
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
                    decrypted[bufferIndex + i] = (byte)(payloadMessage.frmpayload[bufferIndex + i] ^ sBlock[i]);
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
                    decrypted[bufferIndex + i] = (byte)(payloadMessage.frmpayload[bufferIndex + i] ^ sBlock[i]);
                }
            }
            this.lorametadata.decodedData = Encoding.Default.GetString(decrypted);
            return decrypted;
        }
        
        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
