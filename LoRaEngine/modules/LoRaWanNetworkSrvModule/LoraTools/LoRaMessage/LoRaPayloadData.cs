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

    public enum FctrlEnum : short
    {
        FOptLen1 = 0,
        FOptLen2 = 1,
        FOptLen3 = 2,
        FOptLen4 = 4,
        FpendingOrClassB = 16,
        Ack = 32,
        ADRAckReq = 64,
        ADR = 128
    };
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
            MacCommandHolder macHolder = new MacCommandHolder(Fopts.ToArray());
            return macHolder;
        }

        /// <param name="inputMessage"></param>
        public LoRaPayloadData(byte[] inputMessage) : base(inputMessage)
        {
            // in this case the payload is not downlink of our type
            Direction = Mhdr.Span[0] & (1 << 6 - 1);
            // get the address
            byte[] addrbytes = new byte[4];
            Array.Copy(inputMessage, 1, addrbytes, 0, 4);
            // address correct but inversed
            Array.Reverse(addrbytes);
            this.DevAddr = addrbytes;
            // Fctrl Frame Control Octet
            this.Fctrl = new Memory<byte>(inputMessage,5,1);
            int foptsSize = this.Fctrl.Span[0] & 0x0f;
            // Fcnt
            this.Fcnt = new Memory<byte>(inputMessage,6,2);
            // FOpts
            this.Fopts = new Memory<byte>(inputMessage, 8, foptsSize);
            //in this case the message don't have a Fport as the payload is empty
            int fportLength = 1;
            if (inputMessage.Length < 13)
            {
                fportLength = 0;
            }
            // Fport can be empty if no commands! 
            this.Fport = new Memory<byte>(inputMessage,8+foptsSize,fportLength) ;
            // frmpayload
            this.Frmpayload = new Memory<byte>(inputMessage,8+fportLength+foptsSize, inputMessage.Length - 8- fportLength - 4 - foptsSize);
        }

        public enum MType
        {
            JoinRequest,
            JoinAccept=32,
            UnconfirmedDataUp=64,
            UnconfirmedDataDown=96,
            ConfirmedDataUp=128,
            ConfirmedDataDown=160

        }

        public LoRaPayloadData(MType mhdr, byte[] devAddr, byte[] fctrl, byte[] fcnt, byte[] fOpts, byte[] fPort, byte[] frmPayload, int direction) : base()
        {
            int fOptsLen = fOpts == null ? 0 : fOpts.Length;
            int frmPayloadLen = frmPayload == null ? 0 : frmPayload.Length;
            int fPortLen = fPort == null ? 0 : fPort.Length;

            int macPyldSize =  devAddr.Length + fctrl.Length + fcnt.Length + fOptsLen+frmPayloadLen+fPortLen;
            RawMessage = new byte[1 + macPyldSize + 4];
            Mhdr = new Memory<byte>(RawMessage, 0, 1);
            RawMessage[0] = (byte)mhdr;
           // Array.Copy(mhdr, 0, RawMessage, 0, 1);
            Array.Reverse(devAddr);
            DevAddr = new Memory<byte>(RawMessage, 1, 4);
            Array.Copy(devAddr, 0, RawMessage, 1, 4);
            if (fOpts != null)
            {
                fctrl[0] = BitConverter.GetBytes((int)fctrl[0] + fOpts.Length)[0];
            }
            Fctrl = new Memory<byte>(RawMessage, 5, 1);               
            Array.Copy(fctrl, 0, RawMessage, 5, 1);
            Fcnt = new Memory<byte>(RawMessage, 6, 2);
            Array.Copy(fcnt, 0, RawMessage, 6, 2);
            if (fOpts != null)
            {
                Fopts = new Memory<byte>(RawMessage, 8, fOptsLen);
                Array.Copy(fOpts, 0, RawMessage, 8, fOptsLen);
            }
            else
            {
                Fopts = null;
            }
            if (fPort != null)
            {
                Fport = new Memory<byte>(RawMessage, 8 + fOptsLen, fPortLen);
                Array.Copy(fPort, 0, RawMessage, 8 + fOptsLen, fPortLen);
            }
            else
            {
                Fport = null;
            }
            if(frmPayload != null)
            {
                Frmpayload = new Memory<byte>(RawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
                Array.Copy(frmPayload, 0, RawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
            }
            else
            {
                frmPayload = null;
            }
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
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr.Span[3], (byte)DevAddr.Span[2], (byte)DevAddr.Span[1],
                (byte)DevAddr.Span[0], Fcnt.Span[0], Fcnt.Span[1], 0x00, 0x00, 0x00, (byte)(RawMessage.Length - 4)
            };
            var algoinput = block.Concat(RawMessage.Take(RawMessage.Length - 4)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return Mic.ToArray().SequenceEqual(result.Take(4).ToArray());
        }

        public void SetMic(string nwskey)
        {
            var byteMsg= this.GetByteMessage();
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block =
                {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr.Span[3], (byte)DevAddr.Span[2], (byte)DevAddr.Span[1],
                (byte)DevAddr.Span[0], Fcnt.Span[0], Fcnt.Span[1], 0x00, 0x00, 0x00, (byte)byteMsg.Length
            };
            var algoinput = block.Concat(byteMsg.Take(byteMsg.Length)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            var res = result.Take(4).ToArray();
            Array.Copy(result.Take(4).ToArray(), 0, RawMessage, RawMessage.Length - 4, 4);
            Mic = new Memory<byte>(RawMessage, RawMessage.Length - 4, 4);
        }

        public void ChangeEndianess()
        {
            this.DevAddr.Span.Reverse();
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
                    0x01, 0x00, 0x00, 0x00, 0x00, (byte)Direction, (byte)DevAddr.Span[3], (byte)DevAddr.Span[2], (byte)DevAddr.Span[1],
                (byte)DevAddr.Span[0], (byte)Fcnt.Span[0], (byte)Fcnt.Span[1], 0x00, 0x00, 0x00, 0x00
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
                Array.Copy(decrypted, 0, RawMessage, RawMessage.Length-4-decrypted.Length , decrypted.Length);
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
            DevAddr.Span.Reverse();
            messageArray.AddRange(DevAddr.ToArray());
            DevAddr.Span.Reverse();
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
