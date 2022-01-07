// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using LoRaWan;
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
        public bool IsConfirmed => MessageType is MacMessageType.ConfirmedDataDown or MacMessageType.ConfirmedDataUp;

        /// <summary>
        /// Gets a value indicating whether does a Mac command require an answer?.
        /// </summary>
        public bool IsMacAnswerRequired => MacCommands?.FirstOrDefault(x => x.Cid == Cid.LinkCheckCmd) != null;

        public FrameControlFlags FrameControlFlags { get; set; }

        /// <summary>
        /// Indicates if the payload is an confirmation message acknowledgement.
        /// </summary>
        public bool IsUpwardAck => FrameControlFlags.HasFlag(FrameControlFlags.Ack);

        /// <summary>
        /// Gets a value indicating whether indicates if the payload is an confirmation message acknowledgement.
        /// </summary>
        public bool IsAdrAckRequested => FrameControlFlags.HasFlag(FrameControlFlags.AdrAckReq);

        /// <summary>
        /// Gets a value indicating whether the network controls the data rate.
        /// </summary>
        public bool IsDataRateNetworkControlled => FrameControlFlags.HasFlag(FrameControlFlags.Adr);

        /// <summary>
        /// Indicates (downlink only) whether a gateway has more data pending (FPending) to be sent.
        /// </summary>
        public bool IsDownlinkFramePending => FrameControlFlags.HasFlag(FrameControlFlags.DownlinkFramePending);

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
        public FramePort? Fport { get; set; }

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
            MHdr = new MacHeader(RawMessage[0]);

            // in this case the payload is not downlink of our type
            if (MessageType is MacMessageType.ConfirmedDataDown or
                MacMessageType.JoinAccept or
                MacMessageType.UnconfirmedDataDown)
            {
                Direction = 1;
            }
            else
            {
                Direction = 0;
            }

            // Fctrl Frame Control Octet
            (FrameControlFlags, var foptsSize) = FrameControl.Decode(inputMessage[5]);
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
            Fport = fportLength > 0 ? (FramePort)inputMessage[8 + foptsSize] : null;
            // frmpayload
            Frmpayload = new Memory<byte>(inputMessage, 8 + fportLength + foptsSize, inputMessage.Length - 8 - fportLength - 4 - foptsSize);

            // Populate the MacCommands present in the payload.
            if (foptsSize > 0)
            {
                MacCommands = MacCommand.CreateMacCommandFromBytes(Fopts);
            }

            Mic = new Memory<byte>(inputMessage, inputMessage.Length - 4, 4);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Downstream Constructor (build a LoRa Message).
        /// </summary>
        public LoRaPayloadData(MacMessageType messageType, byte[] devAddr, FrameControlFlags fctrlFlags, byte[] fcnt, IEnumerable<MacCommand> macCommands, FramePort? fPort, byte[] frmPayload, int direction, uint? server32bitFcnt = null)
        {
            if (devAddr is null) throw new ArgumentNullException(nameof(devAddr));
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
            var fPortLen = fPort is null ? 0 : 1;

            // TODO If there are mac commands to send and no payload, we need to put the mac commands in the frmpayload.
            if (macBytes.Count > 0 && (frmPayload == null || frmPayload.Length == 0))
            {
                frmPayload = fOpts;
                fOpts = null;
                fOptsLen = 0;
                frmPayloadLen = frmPayload.Length;
                fPortLen = 1;
                fPort = FramePort.MacCommand;
            }

            var macPyldSize = devAddr.Length + FrameControl.Size + fcnt.Length + fOptsLen + frmPayloadLen + fPortLen;
            RawMessage = new byte[1 + macPyldSize + 4];
            MHdr = new MacHeader(messageType);
            RawMessage[0] = (byte)MHdr;
            Array.Reverse(devAddr);
            DevAddr = new Memory<byte>(RawMessage, 1, 4);
            Array.Copy(devAddr, 0, RawMessage, 1, 4);
            FrameControlFlags = fctrlFlags;
            RawMessage[5] = FrameControl.Encode(fctrlFlags, fOpts?.Length ?? 0);
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

            if (fPort is { } someFPort)
            {
                Fport = someFPort;
                RawMessage[8 + fOptsLen] = (byte)someFPort;
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
        /// Serialize a message to be sent downlink on the wire.
        /// </summary>
        /// <param name="appSKey">the app key used for encryption.</param>
        /// <param name="nwkSKey">the nwk key used for encryption.</param>
        /// <returns>the Downlink message.</returns>
        public byte[] Serialize(AppSessionKey appSKey, NetworkSessionKey nwkSKey)
        {

            // It is a Mac Command payload, needs to encrypt with nwkskey
            if (Fport == FramePort.MacCommand)
            {
                _ = Serialize(nwkSKey);
            }
            else
            {
                _ = Serialize(appSKey);
            }

            SetMic(nwkSKey);
            return GetByteMessage();
        }

        /// <summary>
        /// Method to check if the mic is valid.
        /// </summary>
        /// <param name="nwskey">the network security key.</param>
        /// <returns>if the Mic is valid or not.</returns>
        public override bool CheckMic(NetworkSessionKey key, uint? server32BitFcnt = null)
        {
            Ensure32BitFcntValue(server32BitFcnt);
            var byteMsg = GetByteMessage();

            var mac = MacUtilities.GetMac("AESCMAC");
            var rawKey = new byte[NetworkSessionKey.Size];
            _ = key.Write(rawKey);
            mac.Init(new KeyParameter(rawKey));

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

        public void SetMic(NetworkSessionKey nwskey)
        {
            var byteMsg = GetByteMessage();
            var fcntBytes = GetFcntBlockInfo();

            var mac = MacUtilities.GetMac("AESCMAC");
            var rawKey = new byte[NetworkSessionKey.Size];
            _ = nwskey.Write(rawKey);
            var key = new KeyParameter(rawKey);
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
        public byte[] GetDecryptedPayload(NetworkSessionKey key)
        {
            var rawKey = new byte[NetworkSessionKey.Size];
            _ = key.Write(rawKey);
            return GetDecryptedPayload(rawKey);
        }

        /// <summary>
        /// Decrypts the payload value, without changing the <see cref="RawMessage"/>.
        /// </summary>
        /// <remarks>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py.</remarks>
        public byte[] GetDecryptedPayload(AppSessionKey key)
        {
            var rawKey = new byte[AppSessionKey.Size];
            _ = key.Write(rawKey);
            return GetDecryptedPayload(rawKey);
        }

        private byte[] GetDecryptedPayload(byte[] rawSessionKey)
        {
            if (!Frmpayload.Span.IsEmpty)
            {
                try
                {
                    var aesEngine = new AesEngine();
                    aesEngine.Init(true, new KeyParameter(rawSessionKey));
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
                catch (Exception ex)
                {
                    throw new LoRaProcessingException("Failed to decrypt payload.", ex, LoRaProcessingErrorCode.PayloadDecryptionFailed);
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        ///  Replaces the <see cref="Frmpayload"/>, encrypting the values.
        /// </summary>
        public override byte[] Serialize(NetworkSessionKey key) =>
            Serialize(GetDecryptedPayload(key));

        /// <summary>
        ///  Replaces the <see cref="Frmpayload"/>, encrypting the values.
        /// </summary>
        public override byte[] Serialize(AppSessionKey key) =>
            Serialize(GetDecryptedPayload(key));

        private byte[] Serialize(byte[] rawDecryptedPayload)
        {
            if (!Frmpayload.Span.IsEmpty)
            {
                Array.Copy(rawDecryptedPayload, 0, RawMessage, RawMessage.Length - 4 - rawDecryptedPayload.Length, rawDecryptedPayload.Length);
                return rawDecryptedPayload;
            }
            else
            {
                return null;
            }
        }

        public override byte[] GetByteMessage()
        {
            var messageArray = new List<byte>
            {
                (byte)MHdr
            };
            DevAddr.Span.Reverse();
            messageArray.AddRange(DevAddr.ToArray());
            DevAddr.Span.Reverse();
            messageArray.Add(FrameControl.Encode(FrameControlFlags, Fopts.Length));
            messageArray.AddRange(Fcnt.ToArray());
            if (!Fopts.Span.IsEmpty)
            {
                messageArray.AddRange(Fopts.ToArray());
            }

            if (Fport is { } someFport)
            {
                messageArray.Add((byte)someFport);
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

        public override bool RequiresConfirmation
            => IsConfirmed || IsMacAnswerRequired;

        private byte[] GetFcntBlockInfo()
        {
            return Server32BitFcnt ?? (new byte[] { Fcnt.Span[0], Fcnt.Span[1], 0x00, 0x00 });
        }

        public override bool CheckMic(AppKey key) => throw new NotImplementedException();

        public override byte[] PerformEncryption(AppKey key) => throw new NotImplementedException();
    }
}
