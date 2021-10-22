// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Org.BouncyCastle.Crypto.Engines;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// the body of an Uplink (normal) message.
    /// </summary>
    public class LoRaPayloadData : LoRaPayload
    {
        /// <summary>
        /// Gets or sets list of Mac Commands in the LoRaPayload.
        /// </summary>
#pragma warning disable CA2227 // Collection properties should be read only
        // Class is a DTO
        public IList<MacCommand> MacCommands { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        /// <summary>
        /// Gets the LoRa payload fport as value.
        /// </summary>
        [JsonIgnore]
        public byte FPortValue => Fport.Span.Length > 0 ? Fport.Span[0] : (byte)0;

        /// <summary>
        /// Gets the LoRa payload frame counter.
        /// </summary>
        public ushort GetFcnt() => MemoryMarshal.Read<ushort>(Fcnt.Span);

        /// <summary>
        /// Gets the DevAdd netID.
        /// </summary>
        [JsonIgnore]
        public byte DevAddrNetID => (byte)(DevAddr.Span[0] & 0b11111110);

        /// <summary>
        /// Gets a value indicating whether the payload is a confirmation (ConfirmedDataDown or ConfirmedDataUp).
        /// </summary>
        public bool IsConfirmed => LoRaMessageType is LoRaMessageType.ConfirmedDataDown or LoRaMessageType.ConfirmedDataUp;

        /// <summary>
        /// Gets a value indicating whether does a Mac command require an answer?.
        /// </summary>
        public bool IsMacAnswerRequired => MacCommands?.FirstOrDefault(x => x.Cid == Cid.LinkCheckCmd) != null;

        /// <summary>
        /// Indicates if the payload is an confirmation message acknowledgement.
        /// </summary>
        public bool IsUpwardAck() => (Fctrl.Span[0] & (byte)LoRaMessage.Fctrl.Ack) == 32;

        /// <summary>
        /// Gets a value indicating whether indicates if the payload is an confirmation message acknowledgement.
        /// </summary>
        public bool IsAdrReq => (Fctrl.Span[0] & 0b01000000) > 0;

        /// <summary>
        /// Gets a value indicating whether the device has ADR enabled.
        /// </summary>
        public bool IsAdrEnabled => (Fctrl.Span[0] & 0b10000000) > 0;

        /// <summary>
        /// Gets or sets frame control octet.
        /// </summary>
        public Memory<byte> Fctrl { get; set; }

        /// <summary>
        /// Gets or sets frame Counter.
        /// </summary>
        public Memory<byte> Fcnt { get; set; }

        /// <summary>
        /// Gets or sets optional frame.
        /// </summary>
        public Memory<byte> Fopts { get; set; }

        /// <summary>
        /// Gets or sets port field.
        /// </summary>
        public Memory<byte> Fport { get; set; }

        /// <summary>
        /// Gets or sets mAC Frame Payload Encryption.
        /// </summary>
        public Memory<byte> Frmpayload { get; set; }

        /// <summary>
        /// Gets or sets get message direction.
        /// </summary>
        public int Direction { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Constructor used by the simulator.
        /// </summary>
        public LoRaPayloadData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Upstream Constructor (decode a LoRa Message from existing array of bytes).
        /// </summary>
        /// <param name="inputMessage">the upstream Constructor.</param>
        public LoRaPayloadData(byte[] inputMessage)
            : base(inputMessage)
        {
            if (inputMessage is null) throw new ArgumentNullException(nameof(inputMessage));

            // get the address
            var addrbytes = new byte[4];
            Array.Copy(inputMessage, 1, addrbytes, 0, 4);
            // address correct but inversed
            Array.Reverse(addrbytes);
            DevAddr = addrbytes;
            LoRaMessageType = (LoRaMessageType)RawMessage[0];

            // in this case the payload is not downlink of our type
            if (LoRaMessageType is LoRaMessageType.ConfirmedDataDown or
                LoRaMessageType.JoinAccept or
                LoRaMessageType.UnconfirmedDataDown)
            {
                Direction = 1;
            }
            else
            {
                Direction = 0;
            }

            Mhdr = new Memory<byte>(RawMessage, 0, 1);
            // Fctrl Frame Control Octet
            Fctrl = new Memory<byte>(inputMessage, 5, 1);
            var foptsSize = Fctrl.Span[0] & 0x0f;
            // Fcnt
            Fcnt = new Memory<byte>(inputMessage, 6, 2);
            // FOpts
            Fopts = new Memory<byte>(inputMessage, 8, foptsSize);
            // in this case the message don't have a Fport as the payload is empty
            var fportLength = 1;
            if (inputMessage.Length < 13)
            {
                fportLength = 0;
            }

            // Fport can be empty if no commands!
            Fport = new Memory<byte>(inputMessage, 8 + foptsSize, fportLength);
            // frmpayload
            Frmpayload = new Memory<byte>(inputMessage, 8 + fportLength + foptsSize, inputMessage.Length - 8 - fportLength - 4 - foptsSize);

            // Populate the MacCommands present in the payload.
            if (foptsSize > 0)
            {
                MacCommands = MacCommand.CreateMacCommandFromBytes(ConversionHelper.ByteArrayToString(DevAddr), Fopts);
            }

            Mic = new Memory<byte>(inputMessage, inputMessage.Length - 4, 4);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Downstream Constructor (build a LoRa Message).
        /// </summary>
        public LoRaPayloadData(LoRaMessageType mhdr, byte[] devAddr, byte[] fctrl, byte[] fcnt, IEnumerable<MacCommand> macCommands, byte[] fPort, byte[] frmPayload, int direction, uint? server32bitFcnt = null)
        {
            if (devAddr is null) throw new ArgumentNullException(nameof(devAddr));
            if (fctrl is null) throw new ArgumentNullException(nameof(fctrl));
            if (fcnt is null) throw new ArgumentNullException(nameof(fcnt));

            var macBytes = new List<byte>(3);
            if (macCommands != null)
            {
                foreach (var macCommand in macCommands)
                {
                    macBytes.AddRange(macCommand.ToBytes());
                }
            }

            Ensure32BitFcntValue(server32bitFcnt);

            var fOpts = macBytes.ToArray();
            var fOptsLen = fOpts == null ? 0 : fOpts.Length;
            var frmPayloadLen = frmPayload == null ? 0 : frmPayload.Length;
            var fPortLen = fPort == null ? 0 : fPort.Length;

            // TODO If there are mac commands to send and no payload, we need to put the mac commands in the frmpayload.
            if (macBytes.Count > 0 && (frmPayload == null || frmPayload.Length == 0))
            {
                frmPayload = fOpts;
                fOpts = null;
                fOptsLen = 0;
                frmPayloadLen = frmPayload.Length;
                fPortLen = 1;
                fPort = new byte[1] { 0 };
            }

            var macPyldSize = devAddr.Length + fctrl.Length + fcnt.Length + fOptsLen + frmPayloadLen + fPortLen;
            RawMessage = new byte[1 + macPyldSize + 4];
            Mhdr = new Memory<byte>(RawMessage, 0, 1);
            RawMessage[0] = (byte)mhdr;
            LoRaMessageType = mhdr;
            // Array.Copy(mhdr, 0, RawMessage, 0, 1);
            Array.Reverse(devAddr);
            DevAddr = new Memory<byte>(RawMessage, 1, 4);
            Array.Copy(devAddr, 0, RawMessage, 1, 4);
            if (fOpts != null)
            {
                fctrl[0] = BitConverter.GetBytes(fctrl[0] + fOpts.Length)[0];
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

            if (frmPayload != null)
            {
                Frmpayload = new Memory<byte>(RawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
                Array.Copy(frmPayload, 0, RawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
            }

            if (!Frmpayload.Span.IsEmpty)
                Frmpayload.Span.Reverse();
            Direction = direction;
        }

        /// <summary>
        /// Serialize a message to be sent upstream.
        /// </summary>
        public UplinkPktFwdMessage SerializeUplink(string appSKey, string nwkSKey, string datr = "SF10BW125", double freq = 868.3, uint tmst = 0, float lsnr = 0)
        {
            _ = PerformEncryption(appSKey);
            SetMic(nwkSKey);
            return new UplinkPktFwdMessage(GetByteMessage(), datr, freq, tmst, lsnr);
        }

        /// <summary>
        /// Serialize a message to be sent downlink on the wire.
        /// </summary>
        /// <param name="appSKey">the app key used for encryption.</param>
        /// <param name="nwkSKey">the nwk key used for encryption.</param>
        /// <param name="datr">the calculated datarate.</param>
        /// <param name="freq">The frequency at which to be sent.</param>
        /// <param name="tmst">time stamp.</param>
        /// <param name="devEUI">the device EUI.</param>
        /// <returns>the Downlink message.</returns>
        public DownlinkPktFwdMessage Serialize(string appSKey, string nwkSKey, string datr, double freq, long tmst, string devEUI)
        {
            if (devEUI is null) throw new ArgumentNullException(nameof(devEUI));

            // It is a Mac Command payload, needs to encrypt with nwkskey
            if (FPortValue == 0)
            {
                _ = PerformEncryption(nwkSKey);
            }
            else
            {
                _ = PerformEncryption(appSKey);
            }

            SetMic(nwkSKey);
            var downlinkPktFwdMessage = new DownlinkPktFwdMessage(GetByteMessage(), datr, freq, tmst);
            if (Logger.LoggerLevel < LogLevel.Information)
            {
                var jsonMsg = JsonConvert.SerializeObject(downlinkPktFwdMessage);

                if (devEUI.Length != 0)
                {
                    Logger.Log(devEUI, $"{(LoRaMessageType)Mhdr.Span[0]} {jsonMsg}", LogLevel.Debug);
                }
                else
                {
                    Logger.Log(ConversionHelper.ByteArrayToString(DevAddr.Span.ToArray()), $"{(LoRaMessageType)Mhdr.Span[0]} {jsonMsg}", LogLevel.Debug);
                }
            }

            return downlinkPktFwdMessage;
        }

        /// <summary>
        /// Method to check if the mic is valid.
        /// </summary>
        /// <param name="nwskey">the network security key.</param>
        /// <returns>if the Mic is valid or not.</returns>
        public override bool CheckMic(string nwskey, uint? server32BitFcnt = null)
        {
            Ensure32BitFcntValue(server32BitFcnt);
            var byteMsg = GetByteMessage();

            var mac = MacUtilities.GetMac("AESCMAC");
            var key = new KeyParameter(ConversionHelper.StringToByteArray(nwskey));
            mac.Init(key);

            var fcntBytes = GetFcntBlockInfo();

            byte[] block =
            {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, DevAddr.Span[3], DevAddr.Span[2], DevAddr.Span[1],
                DevAddr.Span[0], fcntBytes[0], fcntBytes[1], fcntBytes[2], fcntBytes[3], 0x00, (byte)(byteMsg.Length - 4)
            };
            var algoinput = block.Concat(byteMsg.Take(byteMsg.Length - 4)).ToArray();

            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            var result = MacUtilities.DoFinal(mac);
            return Mic.ToArray().SequenceEqual(result.Take(4).ToArray());
        }

        public void SetMic(string nwskey)
        {
            var byteMsg = GetByteMessage();
            var fcntBytes = GetFcntBlockInfo();

            var mac = MacUtilities.GetMac("AESCMAC");
            var key = new KeyParameter(ConversionHelper.StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block =
            {
                0x49, 0x00, 0x00, 0x00, 0x00, (byte)Direction, DevAddr.Span[3], DevAddr.Span[2], DevAddr.Span[1],
                DevAddr.Span[0], fcntBytes[0], fcntBytes[1], fcntBytes[2], fcntBytes[3], 0x00, (byte)byteMsg.Length
            };
            var algoinput = block.Concat(byteMsg.Take(byteMsg.Length)).ToArray();

            // byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            var result = MacUtilities.DoFinal(mac);
            // var res = result.Take(4).ToArray();
            // Array.Copy(result.Take(4).ToArray(), 0, RawMessage, RawMessage.Length - 4, 4);
            Array.Copy(result, 0, RawMessage, RawMessage.Length - 4, 4);
            Mic = new Memory<byte>(RawMessage, RawMessage.Length - 4, 4);
        }

        public void ChangeEndianess()
        {
            DevAddr.Span.Reverse();
        }

        /// <summary>
        /// Decrypts the payload value, without changing the <see cref="RawMessage"/>.
        /// </summary>
        /// <remarks>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py.</remarks>
        public byte[] GetDecryptedPayload(string appSkey)
        {
            if (!Frmpayload.Span.IsEmpty)
            {
                var aesEngine = new AesEngine();
                var tmp = ConversionHelper.StringToByteArray(appSkey);
                aesEngine.Init(true, new KeyParameter(tmp));
                var fcntBytes = GetFcntBlockInfo();

                byte[] aBlock =
                {
                    0x01, 0x00, 0x00, 0x00, 0x00, (byte)Direction, DevAddr.Span[3], DevAddr.Span[2], DevAddr.Span[1],
                    DevAddr.Span[0], fcntBytes[0], fcntBytes[1], fcntBytes[2], fcntBytes[3], 0x00, 0x00
                };

                byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                var size = Frmpayload.Length;
                var decrypted = new byte[size];
                byte bufferIndex = 0;
                short ctr = 1;
                int i;
                while (size >= 16)
                {
                    aBlock[15] = (byte)(ctr & 0xFF);
                    ctr++;
                    var processed = aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                    if (processed != aBlock.Length) throw new InvalidOperationException($"Failed to process block. Processed length was {processed}");

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
                    var processed = aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                    if (processed != aBlock.Length) throw new InvalidOperationException($"Failed to process block. Processed length was {processed}");

                    for (i = 0; i < size; i++)
                    {
                        decrypted[bufferIndex + i] = (byte)(Frmpayload.Span[bufferIndex + i] ^ sBlock[i]);
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
        ///  Replaces the <see cref="Frmpayload"/>, encrypting the values.
        /// </summary>
        public override byte[] PerformEncryption(string appSkey)
        {
            if (!Frmpayload.Span.IsEmpty)
            {
                var decrypted = GetDecryptedPayload(appSkey);
                Array.Copy(decrypted, 0, RawMessage, RawMessage.Length - 4 - decrypted.Length, decrypted.Length);
                return decrypted;
            }
            else
            {
                return null;
            }
        }

        public override byte[] GetByteMessage()
        {
            var messageArray = new List<byte>();
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

        /// <summary>
        /// Add Mac Command to a LoRa Payload
        /// Warning, do not use this method if your LoRaPayload was created from bytes.
        /// </summary>
        public void AddMacCommand(MacCommand mac)
        {
            if (MacCommands == null)
            {
                MacCommands = new List<MacCommand>();
            }

            MacCommands.Add(mac);
        }

        private byte[] GetFcntBlockInfo()
        {
            return Server32BitFcnt ?? (new byte[] { Fcnt.Span[0], Fcnt.Span[1], 0x00, 0x00 });
        }
    }
}
