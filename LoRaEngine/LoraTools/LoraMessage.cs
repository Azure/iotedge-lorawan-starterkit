using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Linq;

namespace PacketManager
{
    public class LoRaRawMessage
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


        /// <param name="inputMessage"></param>
        public LoRaRawMessage(byte[] inputMessage)
        {
            rawMessage = inputMessage;
            //get the mhdr
            byte[] mhdr = new byte[1];
            Array.Copy(inputMessage, 0, mhdr, 0, 1);
            this.mhdr = mhdr;
            //get direction
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

    /// <summary>
    /// class exposing usefull message stuff
    /// </summary>
    public class LoRaMessage
    {
        public LoRaRawMessage rawMessage;

        public LoRaMessage(byte[] inputMessage)
        {
            //set up the parts of the raw message
            rawMessage = new LoRaRawMessage(inputMessage);

        }

        /// <summary>
        /// Method to check if the mic is valid
        /// </summary>
        /// <param name="nwskey">the network security key</param>
        /// <returns></returns>
        public bool CheckMic(byte[] nwskey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(nwskey);
            mac.Init(key);
            byte[] block = { 0x49, 0x00, 0x00, 0x00, 0x00, (byte)this.rawMessage.direction, (byte)(this.rawMessage.devAddr[3]), (byte)(rawMessage.devAddr[2]), (byte)(rawMessage.devAddr[1]),
                (byte)(rawMessage.devAddr[0]),  this.rawMessage.fcnt[1] , this.rawMessage.fcnt[0],0x00, 0x00, 0x00, (byte)(this.rawMessage.rawMessage.Length-4) };
            var algoinput = block.Concat(this.rawMessage.rawMessage.Take(this.rawMessage.rawMessage.Length - 4)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return this.rawMessage.mic.SequenceEqual(result.Take(4).ToArray());
        }

        /// <summary>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py
        /// </summary>
        public byte[] DecryptPayload(byte[] appSkey)
        {
            AesEngine aesEngine = new AesEngine();
            aesEngine.Init(true, new KeyParameter(appSkey));

            byte[] aBlock = { 0x01, 0x00, 0x00, 0x00, 0x00, (byte)this.rawMessage.direction, (byte)(rawMessage.devAddr[3]), (byte)(rawMessage.devAddr[2]), (byte)(rawMessage.devAddr[1]),
                (byte)(rawMessage.devAddr[0]),  0x00 , 0x00,(byte)(rawMessage.fcnt[1]), (byte)(rawMessage.fcnt[0]), 0x00, 0x00, 0x00 };

            byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            int size = rawMessage.frmpayload.Length;
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
                    decrypted[bufferIndex + i] = (byte)(rawMessage.frmpayload[bufferIndex + i] ^ sBlock[i]);
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
                    decrypted[bufferIndex + i] = (byte)(rawMessage.frmpayload[bufferIndex + i] ^ sBlock[i]);
                }
            }

            return decrypted;
        }
    }
}
