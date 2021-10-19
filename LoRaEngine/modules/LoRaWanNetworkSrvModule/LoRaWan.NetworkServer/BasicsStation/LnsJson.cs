// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Text.Json;
    using LoRaWan;

    public static class LnsJson
    {
        [Flags]
        enum UplinkDataFrameFields { None, MessageType = 1, MacHeader = 2, DevAddr = 4, Mic = 8 }

        public static void ReadUplinkDataFrame(Utf8JsonReader reader,
                                               out MacHeader macHeader,
                                               out DevAddr devAddr,
                                               out Mic mic)
        {
            // TODO adapt to documentation on when to throw "JsonException" vs "NotSupportedException"

            macHeader = default;
            devAddr = default;
            mic = default;

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();
            _ = reader.Read();
            var fields = UplinkDataFrameFields.None;
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals("msgtype"))
                {
                    _ = reader.Read();
                    if (!reader.ValueTextEquals("updf"))
                        throw new JsonException();
                    _ = reader.Read();
                    fields |= UplinkDataFrameFields.MessageType;
                }
                else if (reader.ValueTextEquals("MHdr"))
                {
                    _ = reader.Read();
                    (fields, macHeader) = reader.TryGetByte(out var value) ? (fields | UplinkDataFrameFields.MacHeader, new MacHeader(value)) : throw new JsonException();
                    _ = reader.Read();
                }
                else if (reader.ValueTextEquals("DevAddr"))
                {
                    _ = reader.Read();
                    (fields, devAddr) = reader.TryGetUInt32(out var value) ? (fields | UplinkDataFrameFields.DevAddr, new DevAddr(value)) : throw new JsonException();
                    _ = reader.Read();
                }
                else if (reader.ValueTextEquals("MIC"))
                {
                    _ = reader.Read();
                    (fields, mic) = reader.TryGetInt32(out var value) ? (fields | UplinkDataFrameFields.Mic, new Mic(unchecked((uint)value))) : throw new JsonException();
                    _ = reader.Read();
                }
                else
                {
                    _ = reader.Read();
                    reader.Skip();
                    _ = reader.Read();
                }
            }

            if (fields == UplinkDataFrameFields.None)
                throw new JsonException();
/*
{
  "msgtype"   : "updf",
  "MHdr"      : UINT,
  "DevAddr"   : INT32,
  "FCtrl"     : UINT,
  "FCnt",     : UINT,
  "FOpts"     : "HEX",
  "FPort"     : INT(-1..255),
  "FRMPayload": "HEX",
  "MIC"       : INT32,
  ..
  "DR"    : INT,
  "Freq"  : INT,
  "upinfo": {
    "rctx"    : INT64,
    "xtime"   : INT64,
    "gpstime" : INT64,
    "rssi"    : FLOAT,
    "snr"     : FLOAT
  }
}
 */

        }
    }
}
