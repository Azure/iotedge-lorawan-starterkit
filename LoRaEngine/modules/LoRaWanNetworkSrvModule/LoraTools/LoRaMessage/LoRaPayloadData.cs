using LoRaTools.Utils;
using LoRaWan;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LoRaTools.LoRaMessage
{
    /// <summary>
    /// the body of an Uplink (normal) message
    /// </summary>
    public class LoRaPayloadData : LoRaPayload
    {
        /// <summary>
        /// Frame control octet
        /// </summary>
        public Memory<Byte> Fctrl { get; set; }
        /// <summary>
        /// Frame Counter
        /// </summary>
        public Memory<Byte> Fcnt { get; set; }
        /// <summary>
        /// Optional frame
        /// </summary>
        public Memory<Byte> Fopts { get; set; }
        /// <summary>
        /// Port field
        /// </summary>
        public Memory<Byte> Fport { get; set; }
        /// <summary>
        /// MAC Frame Payload Encryption 
        /// </summary>
        public Memory<Byte> Frmpayload { get; set; }

        /// <summary>
        /// get message direction
        /// </summary>
        public int Direction { get; set; }

        public MacCommandHolder GetMacCommands()
        {
            Logger.Log("fopts : " + Fopts.Length, Logger.LoggingLevel.Full);
            MacCommandHolder macHolder = new MacCommandHolder(Fopts.ToArray());
            return macHolder;
        }

        /// <param name="inputMessage"></param>
        public LoRaPayloadData(byte[] inputMessage) : base(inputMessage)
        {
            // get direction
            var checkDir = Mhdr.Span[0] >> 5;
            // in this case the payload is not downlink of our type


            Direction = Mhdr.Span[0] & (1 << 6 - 1);

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

        public LoRaPayloadData(byte[] mhdr, byte[] devAddr, byte[] fctrl, byte[] fcnt, byte[] fOpts, byte[] fPort, byte[] frmPayload, int direction) : base()
        {
            Mhdr = mhdr;
            Array.Reverse(devAddr);
            DevAddr = devAddr;
            if (fOpts != null)
            {
                fctrl[0] = BitConverter.GetBytes((int)fctrl[0] + fOpts.Length)[0];
            }
            Fctrl = fctrl;
            Fcnt = fcnt;
            Fopts = fOpts;
            Fport = fPort;
            Frmpayload = frmPayload;
            if (!Frmpayload.Span.IsEmpty)
                Frmpayload.Span.Reverse();
            Direction = direction;
        }

        /// <summary>
        /// Method to check if the mic is valid
        /// </summary>
        /// <param name="nwskey">the network security key</param>
        /// <returns>if the Mic is valid or not</returns>
        public override bool CheckMic(string nwskey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block =
                {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr[3], (byte)DevAddr[2], (byte)DevAddr[1],
                (byte)DevAddr[0], Fcnt.Span[0], Fcnt.Span[1], 0x00, 0x00, 0x00, (byte)(RawMessage.Length - 4)
            };
            var algoinput = block.Concat(RawMessage.Take(RawMessage.Length - 4)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return Mic.ToArray().SequenceEqual(result.Take(4).ToArray());
        }

        public void SetMic(string nwskey)
        {
            RawMessage = this.GetByteMessage();
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block =
                {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr[3], (byte)DevAddr[2], (byte)DevAddr[1],
                (byte)DevAddr[0], Fcnt.Span[0], Fcnt.Span[1], 0x00, 0x00, 0x00, (byte)RawMessage.Length
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
            if (!Frmpayload.Span.IsEmpty)
            {
                AesEngine aesEngine = new AesEngine();
                byte[] tmp = ConversionHelper.StringToByteArray(appSkey);

                aesEngine.Init(true, new KeyParameter(tmp));

                byte[] aBlock =
                    {
                    0x01, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr[3], (byte)DevAddr[2], (byte)DevAddr[1],
                (byte)DevAddr[0], (byte)Fcnt.Span[0], (byte)Fcnt.Span[1], 0x00, 0x00, 0x00, 0x00
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
                        decrypted[bufferIndex + i] = (byte)(Frmpayload.Span[bufferIndex + i] ^ sBlock[i]);
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
                        decrypted[bufferIndex + i] = (byte)(Frmpayload.Span[bufferIndex + i] ^ sBlock[i]);
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

        public override byte[] GetByteMessage()
        {
            List<byte> messageArray = new List<byte>();
            messageArray.AddRange(Mhdr.ToArray());
            messageArray.AddRange(DevAddr.Reverse().ToArray());
            messageArray.AddRange(Fctrl.ToArray());
            messageArray.AddRange(Fcnt.ToArray());
            if (!Fopts.Span.IsEmpty)
            {
                messageArray.AddRange(Fopts.ToArray());
            }
            if (!Fport.Span.IsEmpty)
            {
                messageArray.AddRange(Fport.ToArray());
            }
            if (!Frmpayload.Span.IsEmpty)
            {
                messageArray.AddRange(Frmpayload.ToArray());
            }
            if (Mic.Span != null)
            {
                messageArray.AddRange(Mic.Span.ToArray());
            }
            return messageArray.ToArray();
        }


    }

}
