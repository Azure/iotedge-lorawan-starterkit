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
            var messageType = macHeader.MessageType;
            if (messageType is not LoRaMessageType.JoinRequest and
                               not LoRaMessageType.JoinAccept and
                               not LoRaMessageType.UnconfirmedDataUp and
                               not LoRaMessageType.UnconfirmedDataDown and
                               not LoRaMessageType.ConfirmedDataUp and
                               not LoRaMessageType.ConfirmedDataDown)
            {
                throw new NotImplementedException();
            };

            LoRaMessageType = messageType;

            // in this case the payload is not downlink of our type
            Direction = messageType is LoRaMessageType.ConfirmedDataDown or
                                       LoRaMessageType.JoinAccept or
                                       LoRaMessageType.UnconfirmedDataDown ? 1 : 0;

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
