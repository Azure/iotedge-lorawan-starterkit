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
            DevAddr = new byte[LoRaWan.DevAddr.Size];
            _ = devAddress.Write(DevAddr.Span);
            DevAddr.Span.Reverse();

            // Parsing LoRaMessageType in legacy format
            LoRaMessageType = macHeader.MessageType switch
            {
                MacMessageType.JoinRequest => LoRaMessageType.JoinRequest,
                MacMessageType.JoinAccept => LoRaMessageType.JoinAccept,
                MacMessageType.UnconfirmedDataUp => LoRaMessageType.UnconfirmedDataUp,
                MacMessageType.UnconfirmedDataDown => LoRaMessageType.UnconfirmedDataDown,
                MacMessageType.ConfirmedDataUp => LoRaMessageType.ConfirmedDataUp,
                MacMessageType.ConfirmedDataDown => LoRaMessageType.ConfirmedDataDown,
                MacMessageType.RejoinRequest => throw new NotImplementedException(),
                MacMessageType.Proprietary => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };

            // in this case the payload is not downlink of our type
            Direction = macHeader.MessageType is MacMessageType.ConfirmedDataDown or
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
            Fport = new byte[FramePort.Size];
            _ = port.Write(Fport.Span);

            Mic = new byte[LoRaWan.Mic.Size];
            _ = mic.Write(Mic.Span);
        }
    }
}
