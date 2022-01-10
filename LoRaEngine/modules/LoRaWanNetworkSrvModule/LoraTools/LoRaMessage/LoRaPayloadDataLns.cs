// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Buffers.Binary;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public class LoRaPayloadDataLns : LoRaPayloadData
    {
        public LoRaPayloadDataLns(DevAddr devAddr,
                                  MacHeader macHeader,
                                  ushort counter,
                                  string options,
                                  string payload,
                                  Mic mic)
            : this(devAddr, macHeader, default, counter, options, payload, default, mic, default) { }

        public LoRaPayloadDataLns(DevAddr devAddress,
                                  MacHeader macHeader,
                                  FrameControlFlags fctrlFlags,
                                  ushort counter,
                                  string options,
                                  string payload,
                                  FramePort port,
                                  Mic mic,
                                  ILogger logger)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(payload)) throw new ArgumentNullException(nameof(payload));

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

            MessageType = messageType;

            // in this case the payload is not downlink of our type
            Direction = messageType is MacMessageType.ConfirmedDataDown or
                                       MacMessageType.JoinAccept or
                                       MacMessageType.UnconfirmedDataDown ? 1 : 0;

            // Setting MHdr value
            Mhdr = new byte[1];
            _ = macHeader.Write(Mhdr.Span);

            // Setting Fctrl
            FrameControlFlags = fctrlFlags;

            // Setting Fcnt
            Fcnt = new byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(Fcnt.Span, counter);

            // Setting FOpts
            Fopts = new byte[options.Length / 2];
            _ = Hexadecimal.TryParse(options, Fopts.Span);

            // Populate the MacCommands present in the payload.
            if (options.Length > 0)
            {
                MacCommands = MacCommand.CreateMacCommandFromBytes(Fopts, logger);
            }

            // Setting FRMPayload
            Frmpayload = new byte[payload.Length / 2];
            _ = Hexadecimal.TryParse(payload, Frmpayload.Span);

            // Fport can be empty if no commands
            Fport = port;

            Mic = mic;
        }
    }
}
