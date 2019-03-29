// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Mac;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Engines;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// the body of an Uplink (normal) message
    /// </summary>
    public class LoRaPayloadData : LoRaPayload
    {
        /// <summary>
        /// Gets or sets list of Mac Commands in the LoRaPayload
        /// </summary>
        public List<MacCommand> MacCommands { get; set; }

        /// <summary>
        /// Gets the LoRa payload fport as value
        /// </summary>
        public byte GetFPort()
        {
            byte fportUp = 0;
            if (this.Fport.Span.Length > 0)
            {
                fportUp = this.Fport.Span[0];
            }

            return fportUp;
        }

        /// <summary>
        /// Gets the LoRa payload frame counter
        /// </summary>
        public ushort GetFcnt() => MemoryMarshal.Read<ushort>(this.Fcnt.Span);

        /// <summary>
        /// Gets the DevAdd netID
        /// </summary>
        public byte GetDevAddrNetID() => (byte)(this.DevAddr.Span[0] & 0b11111110);

        /// <summary>
        /// Gets a value indicating whether the payload is a confirmation (ConfirmedDataDown or ConfirmedDataUp)
        /// </summary>
        public bool IsConfirmed => this.LoRaMessageType == LoRaMessageType.ConfirmedDataDown || this.LoRaMessageType == LoRaMessageType.ConfirmedDataUp;

        /// <summary>
        /// Gets a value indicating whether does a Mac command require an answer?
        /// </summary>
        public bool IsMacAnswerRequired => this.MacCommands?.FirstOrDefault(x => x.Cid == CidEnum.LinkCheckCmd) != null;

        /// <summary>
        /// Indicates if the payload is an confirmation message acknowledgement
        /// </summary>
        public bool IsUpwardAck() => (this.Fctrl.Span[0] & (byte)FctrlEnum.Ack) == 32;

        /// <summary>
        /// Gets a value indicating whether indicates if the payload is an confirmation message acknowledgement
        /// </summary>
        public bool IsAdrReq => (this.Fctrl.Span[0] & 0b01000000) > 0;

        /// <summary>
        /// Gets a value indicating whether the device has ADR enabled
        /// </summary>
        public bool IsAdrEnabled => (this.Fctrl.Span[0] & 0b10000000) > 0;

        /// <summary>
        /// Gets or sets frame control octet
        /// </summary>
        public Memory<byte> Fctrl { get; set; }

        /// <summary>
        /// Gets or sets frame Counter
        /// </summary>
        public Memory<byte> Fcnt { get; set; }

        /// <summary>
        /// Gets or sets optional frame
        /// </summary>
        public Memory<byte> Fopts { get; set; }

        /// <summary>
        /// Gets or sets port field
        /// </summary>
        public Memory<byte> Fport { get; set; }

        /// <summary>
        /// Gets or sets mAC Frame Payload Encryption
        /// </summary>
        public Memory<byte> Frmpayload { get; set; }

        /// <summary>
        /// Gets or sets get message direction
        /// </summary>
        public int Direction { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Constructor used by the simulator
        /// </summary>
        public LoRaPayloadData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Upstream Constructor (decode a LoRa Message from existing array of bytes)
        /// </summary>
        /// <param name="inputMessage">the upstream Constructor</param>
        public LoRaPayloadData(byte[] inputMessage)
            : base(inputMessage)
        {
            // get the address
            byte[] addrbytes = new byte[4];
            Array.Copy(inputMessage, 1, addrbytes, 0, 4);
            // address correct but inversed
            Array.Reverse(addrbytes);
            this.DevAddr = addrbytes;
            this.LoRaMessageType = (LoRaMessageType)this.RawMessage[0];

            // in this case the payload is not downlink of our type
            if (this.LoRaMessageType == LoRaMessageType.ConfirmedDataDown ||
                this.LoRaMessageType == LoRaMessageType.JoinAccept ||
                this.LoRaMessageType == LoRaMessageType.UnconfirmedDataDown)
            {
                this.Direction = 1;
            }
            else
            {
                this.Direction = 0;
            }

            this.Mhdr = new Memory<byte>(this.RawMessage, 0, 1);
            // Fctrl Frame Control Octet
            this.Fctrl = new Memory<byte>(inputMessage, 5, 1);
            int foptsSize = this.Fctrl.Span[0] & 0x0f;
            // Fcnt
            this.Fcnt = new Memory<byte>(inputMessage, 6, 2);
            // FOpts
            this.Fopts = new Memory<byte>(inputMessage, 8, foptsSize);
            // in this case the message don't have a Fport as the payload is empty
            int fportLength = 1;
            if (inputMessage.Length < 13)
            {
                fportLength = 0;
            }

            // Fport can be empty if no commands!
            this.Fport = new Memory<byte>(inputMessage, 8 + foptsSize, fportLength);
            // frmpayload
            this.Frmpayload = new Memory<byte>(inputMessage, 8 + fportLength + foptsSize, inputMessage.Length - 8 - fportLength - 4 - foptsSize);

            // Populate the MacCommands present in the payload.
            if (foptsSize > 0)
            {
                this.MacCommands = MacCommand.CreateMacCommandFromBytes(ConversionHelper.ByteArrayToString(this.DevAddr), this.Fopts);
            }

            this.Mic = new Memory<byte>(inputMessage, inputMessage.Length - 4, 4);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Downstream Constructor (build a LoRa Message)
        /// </summary>
        public LoRaPayloadData(LoRaMessageType mhdr, byte[] devAddr, byte[] fctrl, byte[] fcnt, IEnumerable<MacCommand> macCommands, byte[] fPort, byte[] frmPayload, int direction, uint? server32bitFcnt = null)
        {
            List<byte> macBytes = new List<byte>(3);
            if (macCommands != null)
            {
                foreach (var macCommand in macCommands)
                {
                    macBytes.AddRange(macCommand.ToBytes());
                }
            }

            this.Ensure32BitFcntValue(server32bitFcnt);

            var fOpts = macBytes.ToArray();
            int fOptsLen = fOpts == null ? 0 : fOpts.Length;
            int frmPayloadLen = frmPayload == null ? 0 : frmPayload.Length;
            int fPortLen = fPort == null ? 0 : fPort.Length;

            // TODO If there are mac commands to send and no payload, we need to put the mac commands in the frmpayload.
            if (macBytes.Count > 0 && (frmPayload == null || frmPayload?.Count() == 0))
            {
                frmPayload = fOpts;
                fOpts = null;
                fOptsLen = 0;
                frmPayloadLen = frmPayload.Count();
                fPortLen = 1;
                fPort = new byte[1] { 0 };
            }

            int macPyldSize = devAddr.Length + fctrl.Length + fcnt.Length + fOptsLen + frmPayloadLen + fPortLen;
            this.RawMessage = new byte[1 + macPyldSize + 4];
            this.Mhdr = new Memory<byte>(this.RawMessage, 0, 1);
            this.RawMessage[0] = (byte)mhdr;
            this.LoRaMessageType = mhdr;
            // Array.Copy(mhdr, 0, RawMessage, 0, 1);
            Array.Reverse(devAddr);
            this.DevAddr = new Memory<byte>(this.RawMessage, 1, 4);
            Array.Copy(devAddr, 0, this.RawMessage, 1, 4);
            if (fOpts != null)
            {
                fctrl[0] = BitConverter.GetBytes(fctrl[0] + fOpts.Length)[0];
            }

            this.Fctrl = new Memory<byte>(this.RawMessage, 5, 1);
            Array.Copy(fctrl, 0, this.RawMessage, 5, 1);
            this.Fcnt = new Memory<byte>(this.RawMessage, 6, 2);
            Array.Copy(fcnt, 0, this.RawMessage, 6, 2);
            if (fOpts != null)
            {
                this.Fopts = new Memory<byte>(this.RawMessage, 8, fOptsLen);
                Array.Copy(fOpts, 0, this.RawMessage, 8, fOptsLen);
            }
            else
            {
                this.Fopts = null;
            }

            if (fPort != null)
            {
                this.Fport = new Memory<byte>(this.RawMessage, 8 + fOptsLen, fPortLen);
                Array.Copy(fPort, 0, this.RawMessage, 8 + fOptsLen, fPortLen);
            }
            else
            {
                this.Fport = null;
            }

            if (frmPayload != null)
            {
                this.Frmpayload = new Memory<byte>(this.RawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
                Array.Copy(frmPayload, 0, this.RawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
            }
            else
            {
                frmPayload = null;
            }

            if (!this.Frmpayload.Span.IsEmpty)
                this.Frmpayload.Span.Reverse();
            this.Direction = direction;
        }

        /// <summary>
        /// Serialize a message to be sent upstream.
        /// </summary>
        public UplinkPktFwdMessage SerializeUplink(string appSKey, string nwkSKey, string datr = "SF10BW125", double freq = 868.3, uint tmst = 0, float lsnr = 0)
        {
            this.PerformEncryption(appSKey);
            this.SetMic(nwkSKey);
            return new UplinkPktFwdMessage(this.GetByteMessage(), datr, freq, tmst, lsnr);
        }

        /// <summary>
        /// Serialize a message to be sent downlink on the wire.
        /// </summary>
        /// <param name="appSKey">the app key used for encryption</param>
        /// <param name="nwkSKey">the nwk key used for encryption</param>
        /// <param name="datr">the calculated datarate</param>
        /// <param name="freq">The frequency at which to be sent</param>
        /// <param name="tmst">time stamp</param>
        /// <param name="devEUI">the device EUI</param>
        /// <returns>the Downlink message</returns>
        public DownlinkPktFwdMessage Serialize(string appSKey, string nwkSKey, string datr, double freq, long tmst, string devEUI)
        {
            // It is a Mac Command payload, needs to encrypt with nwkskey
            if (this.GetFPort() == 0)
            {
                this.PerformEncryption(nwkSKey);
            }
            else
            {
                this.PerformEncryption(appSKey);
            }

            this.SetMic(nwkSKey);
            var downlinkPktFwdMessage = new DownlinkPktFwdMessage(this.GetByteMessage(), datr, freq, tmst);
            if (Logger.LoggerLevel < LogLevel.Information)
            {
                var jsonMsg = JsonConvert.SerializeObject(downlinkPktFwdMessage);

                if (devEUI.Length != 0)
                {
                    Logger.Log(devEUI, $"{((LoRaMessageType)this.Mhdr.Span[0]).ToString()} {jsonMsg}", LogLevel.Debug);
                }
                else
                {
                    Logger.Log(ConversionHelper.ByteArrayToString(this.DevAddr.Span.ToArray()), $"{((LoRaMessageType)this.Mhdr.Span[0]).ToString()} {jsonMsg}", LogLevel.Debug);
                }
            }

            return downlinkPktFwdMessage;
        }

        /// <summary>
        /// Method to check if the mic is valid
        /// </summary>
        /// <param name="nwskey">the network security key</param>
        /// <returns>if the Mic is valid or not</returns>
        public override bool CheckMic(string nwskey, uint? server32BitFcnt = null)
        {
            this.Ensure32BitFcntValue(server32BitFcnt);
            var byteMsg = this.GetByteMessage();

            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(nwskey));
            mac.Init(key);

            var fcntBytes = this.GetFcntBlockInfo();

            byte[] block =
            {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)this.Direction, this.DevAddr.Span[3], this.DevAddr.Span[2], this.DevAddr.Span[1],
                this.DevAddr.Span[0], fcntBytes[0], fcntBytes[1], fcntBytes[2], fcntBytes[3], 0x00, (byte)(byteMsg.Length - 4)
            };
            var algoinput = block.Concat(byteMsg.Take(byteMsg.Length - 4)).ToArray();

            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return this.Mic.ToArray().SequenceEqual(result.Take(4).ToArray());
        }

        public void SetMic(string nwskey)
        {
            var byteMsg = this.GetByteMessage();
            var fcntBytes = this.GetFcntBlockInfo();

            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(ConversionHelper.StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block =
                {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)this.Direction, this.DevAddr.Span[3], this.DevAddr.Span[2], this.DevAddr.Span[1],
                this.DevAddr.Span[0], fcntBytes[0], fcntBytes[1], fcntBytes[2], fcntBytes[3], 0x00, (byte)byteMsg.Length
            };
            var algoinput = block.Concat(byteMsg.Take(byteMsg.Length)).ToArray();

            // byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            var result = MacUtilities.DoFinal(mac);
            // var res = result.Take(4).ToArray();
            // Array.Copy(result.Take(4).ToArray(), 0, RawMessage, RawMessage.Length - 4, 4);
            Array.Copy(result, 0, this.RawMessage, this.RawMessage.Length - 4, 4);
            this.Mic = new Memory<byte>(this.RawMessage, this.RawMessage.Length - 4, 4);
        }

        public void ChangeEndianess()
        {
            this.DevAddr.Span.Reverse();
        }

        /// <summary>
        /// Decrypts the payload value, without changing the <see cref="RawMessage"/>
        /// </summary>
        /// <remarks>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py</remarks>
        public byte[] GetDecryptedPayload(string appSkey)
        {
            if (!this.Frmpayload.Span.IsEmpty)
            {
                AesEngine aesEngine = new AesEngine();
                byte[] tmp = ConversionHelper.StringToByteArray(appSkey);
                aesEngine.Init(true, new KeyParameter(tmp));
                var fcntBytes = this.GetFcntBlockInfo();

                byte[] aBlock =
                    {
                    0x01, 0x00, 0x00, 0x00, 0x00, (byte)this.Direction, this.DevAddr.Span[3], this.DevAddr.Span[2], this.DevAddr.Span[1],
                    this.DevAddr.Span[0], fcntBytes[0], fcntBytes[1], fcntBytes[2], fcntBytes[3], 0x00, 0x00
                };

                byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                int size = this.Frmpayload.Length;
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
                        decrypted[bufferIndex + i] = (byte)(this.Frmpayload.Span[bufferIndex + i] ^ sBlock[i]);
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
                        decrypted[bufferIndex + i] = (byte)(this.Frmpayload.Span[bufferIndex + i] ^ sBlock[i]);
                    }
                }

                return decrypted;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        ///  Replaces the <see cref="Frmpayload"/>, encrypting the values
        /// </summary>
        public override byte[] PerformEncryption(string appSkey)
        {
            if (!this.Frmpayload.Span.IsEmpty)
            {
                var decrypted = this.GetDecryptedPayload(appSkey);
                Array.Copy(decrypted, 0, this.RawMessage, this.RawMessage.Length - 4 - decrypted.Length, decrypted.Length);
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
            messageArray.AddRange(this.Mhdr.ToArray());
            this.DevAddr.Span.Reverse();
            messageArray.AddRange(this.DevAddr.ToArray());
            this.DevAddr.Span.Reverse();
            messageArray.AddRange(this.Fctrl.ToArray());
            messageArray.AddRange(this.Fcnt.ToArray());
            if (!this.Fopts.Span.IsEmpty)
            {
                messageArray.AddRange(this.Fopts.ToArray());
            }

            if (!this.Fport.Span.IsEmpty)
            {
                messageArray.AddRange(this.Fport.ToArray());
            }

            if (!this.Frmpayload.Span.IsEmpty)
            {
                messageArray.AddRange(this.Frmpayload.ToArray());
            }

            if (this.Mic.Span != null)
            {
                messageArray.AddRange(this.Mic.Span.ToArray());
            }

            return messageArray.ToArray();
        }

        /// <summary>
        /// Add Mac Command to a LoRa Payload
        /// Warning, do not use this method if your LoRaPayload was created from bytes
        /// </summary>
        public void AddMacCommand(MacCommand mac)
        {
            if (this.MacCommands == null)
            {
                this.MacCommands = new List<MacCommand>();
            }

            this.MacCommands.Add(mac);
        }

        private byte[] GetFcntBlockInfo()
        {
            return (this.Server32BitFcnt != null) ? this.Server32BitFcnt : new byte[] { this.Fcnt.Span[0], this.Fcnt.Span[1], 0x00, 0x00 };
        }
    }
}
