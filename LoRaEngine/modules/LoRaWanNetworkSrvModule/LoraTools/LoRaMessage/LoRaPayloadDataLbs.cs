// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using LoRaTools.Utils;
    using LoRaWan;
    using System;
    using System.Linq;

    public class LoRaPayloadDataLbs : LoRaPayloadData
    {
        public LoRaPayloadDataLbs(DevAddr devAddr,
                                  MacHeader mhdr,
                                  FrameControl fctrl,
                                  ushort fcnt,
                                  string fopts,
                                  string frmPayload,
                                  FramePort framePort,
                                  Mic mic)
        {
            if (string.IsNullOrEmpty(frmPayload)) throw new ArgumentNullException(nameof(frmPayload));
            var reversedAddr = devAddr.AsByteArray().Reverse();
            DevAddr = reversedAddr.ToArray();

            LoRaMessageType = mhdr.MessageType switch
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

            Mhdr = new byte[1];
            _ = mhdr.Write(Mhdr.Span);
            // Fctrl Frame Control Octet
            Fctrl = new byte[1];
            _ = fctrl.Write(Fctrl.Span);
            // Fcnt
            Fcnt = BitConverter.GetBytes(fcnt);

            // FOpts
            var foptsSize = fctrl.OptionsLength;
            Fopts = new byte[foptsSize];
            _ = Hexadecimal.TryParse(fopts, Fopts.Span);
            // frmpayload
            Frmpayload = new byte[frmPayload.Length / 2];
            _ = Hexadecimal.TryParse(frmPayload, Frmpayload.Span);

            // Fport can be empty if no commands!
            Fport = new byte[FramePort.Size];
            _ = framePort.Write(Fport.Span);

            // Populate the MacCommands present in the payload.
            if (foptsSize > 0)
            {
                MacCommands = MacCommand.CreateMacCommandFromBytes(ConversionHelper.ByteArrayToString(DevAddr), Fopts);
            }

            Mic = mic.AsByteArray();
        }
    }
}
