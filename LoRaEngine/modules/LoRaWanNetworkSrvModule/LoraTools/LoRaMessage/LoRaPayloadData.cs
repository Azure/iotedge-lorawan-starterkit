// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Org.BouncyCastle.Crypto.Engines;
    using Org.BouncyCastle.Crypto.Parameters;

    /// <summary>
    /// the body of an Uplink (normal) message.
    /// </summary>
    public class LoRaPayloadData : LoRaPayload
    {
        private readonly byte[] rawMessage;

        /// <summary>
        /// Gets or sets list of Mac Commands in the LoRaPayload.
        /// </summary>
#pragma warning disable CA2227 // Collection properties should be read only
        // Class is a DTO
        public IList<MacCommand> MacCommands { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        /// <summary>
        /// Gets a value indicating whether the payload is a confirmation (ConfirmedDataDown or ConfirmedDataUp).
        /// </summary>
        public bool IsConfirmed => MessageType is MacMessageType.ConfirmedDataDown or MacMessageType.ConfirmedDataUp;

        /// <summary>
        /// Gets a value indicating whether does a Mac command require an answer?.
        /// </summary>
        public bool IsMacAnswerRequired => MacCommands?.FirstOrDefault(x => x.Cid == Cid.LinkCheckCmd) != null;

        public FrameControlFlags FrameControlFlags { get; }

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
        public ushort Fcnt { get; }

        /// <summary>
        /// Gets or sets optional frame.
        /// </summary>
        public ReadOnlyMemory<byte> Fopts { get; }

        /// <summary>
        /// Gets or sets port field.
        /// </summary>
        public FramePort? Fport { get; }

        /// <summary>
        /// Gets or sets mAC Frame Payload Encryption.
        /// </summary>
        public ReadOnlyMemory<byte> Frmpayload { get; }

        /// <summary>
        /// Gets or sets get message direction.
        /// </summary>
        public int Direction { get; }

        public LoRaPayloadData(DevAddr devAddress,
                               MacHeader macHeader,
                               FrameControlFlags fctrlFlags,
                               ushort counter,
                               string options,
                               string payload,
                               FramePort? port,
                               Mic? mic,
                               ILogger logger)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (payload is null) throw new ArgumentNullException(nameof(payload));

            // Writing the DevAddr
            DevAddr = devAddress;

            // Parsing LoRaMessageType in legacy format
            var messageType = macHeader.MessageType;
            if (messageType is not MacMessageType.JoinRequest and
                               not MacMessageType.JoinAccept and
                               not MacMessageType.UnconfirmedDataUp and
                               not MacMessageType.UnconfirmedDataDown and
                               not MacMessageType.ConfirmedDataUp and
                               not MacMessageType.ConfirmedDataDown)
            {
                throw new NotImplementedException();
            };

            // in this case the payload is not downlink of our type
            Direction = messageType is MacMessageType.ConfirmedDataDown or
                                       MacMessageType.JoinAccept or
                                       MacMessageType.UnconfirmedDataDown ? 1 : 0;

            // Setting MHdr value
            MHdr = macHeader;

            // Setting Fctrl
            FrameControlFlags = fctrlFlags;

            // Setting Fcnt
            Fcnt = counter;

            // Setting FOpts
            var foptsBytes = new byte[options.Length / 2];
            Fopts = foptsBytes;
            _ = Hexadecimal.TryParse(options, foptsBytes);

            // Populate the MacCommands present in the payload.
            if (options.Length > 0)
            {
                MacCommands = MacCommand.CreateMacCommandFromBytes(Fopts, logger);
            }

            // Setting FRMPayload
            var payloadBytes = new byte[payload.Length / 2];
            Frmpayload = payloadBytes;
            _ = Hexadecimal.TryParse(payload, payloadBytes);

            // Fport can be empty if no commands
            Fport = port;

            Mic = mic;
        }

        public LoRaPayloadData(ReadOnlyMemory<byte> inputMessage) : this(inputMessage.ToArray())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Upstream Constructor (decode a LoRa Message from existing array of bytes).
        /// </summary>
        /// <param name="inputMessage">the upstream Constructor.</param>
        private LoRaPayloadData(byte[] inputMessage)
        {
            this.rawMessage = inputMessage ?? throw new ArgumentNullException(nameof(inputMessage));

            DevAddr = DevAddr.Read(inputMessage.AsSpan(1));
            MHdr = new MacHeader(inputMessage[0]);

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
            Fcnt = BinaryPrimitives.ReadUInt16LittleEndian(inputMessage.AsSpan()[6..]);
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

            Mic = LoRaWan.Mic.Read(inputMessage.AsSpan(inputMessage.Length - 4, 4));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadData"/> class.
        /// Downstream Constructor (build a LoRa Message).
        /// </summary>
        public LoRaPayloadData(MacMessageType messageType, DevAddr devAddr, FrameControlFlags fctrlFlags, ushort fcnt, IEnumerable<MacCommand> macCommands, FramePort? fPort, byte[] frmPayload, int direction, uint? server32bitFcnt = null)
        {
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

            var macPyldSize = DevAddr.Size + FrameControl.Size + sizeof(ushort) + fOptsLen + frmPayloadLen + fPortLen;
            var rawMessage = this.rawMessage = new byte[1 + macPyldSize + 4];
            MHdr = new MacHeader(messageType);
            rawMessage[0] = (byte)MHdr;
            DevAddr = devAddr;
            _ = devAddr.Write(rawMessage.AsSpan(1));
            FrameControlFlags = fctrlFlags;
            rawMessage[5] = FrameControl.Encode(fctrlFlags, fOpts?.Length ?? 0);
            BinaryPrimitives.WriteUInt16LittleEndian(rawMessage.AsSpan()[6..], fcnt);
            Fcnt = fcnt;
            if (fOpts != null)
            {
                Fopts = new Memory<byte>(rawMessage, 8, fOptsLen);
                Array.Copy(fOpts, 0, rawMessage, 8, fOptsLen);
            }
            else
            {
                Fopts = null;
            }

            if (fPort is { } someFPort)
            {
                Fport = someFPort;
                rawMessage[8 + fOptsLen] = (byte)someFPort;
            }
            else
            {
                Fport = null;
            }

            if (frmPayload != null)
            {
                Frmpayload = new Memory<byte>(rawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
                Array.Copy(frmPayload, 0, rawMessage, 8 + fOptsLen + fPortLen, frmPayloadLen);
            }

            Direction = direction;
        }

        /// <summary>
        /// The 32bit Frame counter to be used
        /// in the block if we are in 32bit mode.
        /// </summary>
        private uint? server32BitFcnt;

        public void Reset32BitFcnt() => this.server32BitFcnt = null;
        private void Ensure32BitFcntValue(uint? value) => this.server32BitFcnt ??= value;

        /// <summary>
        /// In 32bit mode, the server needs to infer the upper 16bits by observing
        /// the traffic between the device and the server. We keep a 32bit counter
        /// on the server and combine the upper 16bits with what the client sends us
        /// on the wire (lower 16bits). The result is the inferred counter as we
        /// assume it is on the client.
        /// </summary>
        /// <param name="payloadFcnt">16bits counter sent in the package.</param>
        /// <param name="fcnt">Current server frame counter holding 32bits.</param>
        /// <returns>The inferred 32bit framecounter value, with the higher 16bits holding the server
        /// observed counter information and the lower 16bits the information we got on the wire.</returns>
        public static uint InferUpper32BitsForClientFcnt(ushort payloadFcnt, uint fcnt)
        {
            const uint MaskHigher16 = 0xFFFF0000;

            // server represents the counter in 32bit so does the client, but only sends the lower 16bits
            // infering the upper 16bits from the current count
            var fcntServerUpper = fcnt & MaskHigher16;
            return fcntServerUpper | payloadFcnt;
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
        public bool CheckMic(NetworkSessionKey key, uint? server32BitFcnt = null)
        {
            Ensure32BitFcntValue(server32BitFcnt);
            // do not include MIC as it was already set
            var byteMsg = GetByteMessage()[..^4].ToArray();
            var fcnt = this.server32BitFcnt ?? Fcnt;
            return Mic == LoRaWan.Mic.ComputeForData(key, (byte)Direction, DevAddr, fcnt, byteMsg);
        }

        public void SetMic(NetworkSessionKey nwskey)
        {
            var byteMsg = GetByteMessage();
            var fcnt = this.server32BitFcnt ?? Fcnt;
            Mic = LoRaWan.Mic.ComputeForData(nwskey, (byte)Direction, DevAddr, fcnt, byteMsg);
            _ = Mic.Value.Write(this.rawMessage.AsSpan(this.rawMessage.Length - 4, 4));
        }

        /// <summary>
        /// Decrypts the payload value, without changing the raw message.
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
        /// Decrypts the payload value, without changing the raw message.
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

                    byte[] aBlock =
                    {
                        0x01,
                        0x00, 0x00, 0x00, 0x00,
                        (byte)Direction,
                        /* DevAddr */0x00, 0x00, 0x00, 0x00,
                        /* FCnt */0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00
                    };
                    var pt = DevAddr.Write(aBlock.AsSpan(6));
                    BinaryPrimitives.WriteUInt32LittleEndian(pt, Fcnt);

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
        public byte[] Serialize(NetworkSessionKey key) =>
            Serialize(GetDecryptedPayload(key));

        /// <summary>
        ///  Replaces the <see cref="Frmpayload"/>, encrypting the values.
        /// </summary>
        public byte[] Serialize(AppSessionKey key) =>
            Serialize(GetDecryptedPayload(key));

        private byte[] Serialize(byte[] rawDecryptedPayload)
        {
            if (!Frmpayload.Span.IsEmpty)
            {
                Array.Copy(rawDecryptedPayload, 0, this.rawMessage, this.rawMessage.Length - 4 - rawDecryptedPayload.Length, rawDecryptedPayload.Length);
                return rawDecryptedPayload;
            }
            else
            {
                return null;
            }
        }

        private byte[] GetByteMessage()
        {
            var messageArray = new List<byte>
            {
                (byte)MHdr
            };
            Span<byte> devAddrBytes = stackalloc byte[DevAddr.Size];
            _ = DevAddr.Write(devAddrBytes);
            foreach (var b in devAddrBytes)
                messageArray.Add(b);
            messageArray.Add(FrameControl.Encode(FrameControlFlags, Fopts.Length));
            Span<byte> fcntBytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(fcntBytes, Fcnt);
            messageArray.Add(fcntBytes[0]);
            messageArray.Add(fcntBytes[1]);
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

            if (Mic is { } someMic)
            {
                var micBytes = new byte[4];
                _ = someMic.Write(micBytes);
                messageArray.AddRange(micBytes);
            }

            return messageArray.ToArray();
        }

        public bool RequiresConfirmation => IsConfirmed || IsMacAnswerRequired;
    }
}
