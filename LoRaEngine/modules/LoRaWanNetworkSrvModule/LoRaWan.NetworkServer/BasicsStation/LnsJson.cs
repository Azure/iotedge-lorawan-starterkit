// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using LoRaWan;

    public static class LnsJson
    {
        public static string WriteRouterConfig((Hertz Min, Hertz Max) freqRange,
                                               IEnumerable<(JoinEui Min, JoinEui Max)> joinEuiRanges)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            WriteRouterConfig(writer, freqRange, joinEuiRanges);
            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        public static void WriteRouterConfig(Utf8JsonWriter writer,
                                             (Hertz Min, Hertz Max) freqRange,
                                             IEnumerable<(JoinEui Min, JoinEui Max)> joinEuiRanges)
        {
            writer.WriteStartObject();

            writer.WriteString("msgtype", "router_config");
            Tuple(writer, "freq_range", freqRange, h => h.AsUInt64);
            Array(writer, "JoinEUI", joinEuiRanges, (w, r) => Tuple(w, r, (w, eui) => w.WriteNumberValue(eui.AsUInt64)));

//            Sx1301Conf(writer, //* ... */);

            writer.WriteEndObject();
        }

        public static void Sx1301Conf(Utf8JsonWriter writer,
                                      bool enable, int centreFreq, int rfChain)
        {
            /*
{
  "chip_enable"      : BOOL
  "chip_center_freq" : INT
  "chip_rf_chain"    : INT
  "chan_multiSF_X"   : CHANCONF   // where X in {0..7}
  "chan_LoRa_std"    : CHANCONF
  "chan_FSK"         : CHANCONF
}
             */
        }

        static void Tuple<T>(Utf8JsonWriter writer, string name, (T, T) value, Func<T, ulong> f)
        {
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            var (a, b) = value;
            writer.WriteNumberValue(f(a));
            writer.WriteNumberValue(f(b));
            writer.WriteEndArray();
        }

        static void Array<T>(Utf8JsonWriter writer, string name, IEnumerable<T> items, Action<Utf8JsonWriter, T> f)
        {
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            foreach (var item in items)
                f(writer, item);
            writer.WriteEndArray();
        }

        static void Tuple<T>(Utf8JsonWriter writer, (T, T) value, Action<Utf8JsonWriter, T> f)
        {
            writer.WriteStartArray();
            var (a, b) = value;
            f(writer, a);
            f(writer, b);
            writer.WriteEndArray();
        }

        public static void ReadRouter(Utf8JsonReader reader,
                                      out StationEui stationEui)
        {
            // TODO adapt to documentation on when to throw "JsonException" vs "NotSupportedException"

            stationEui = default;

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();
            _ = reader.Read();
            var readRouter = false;
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals("router"))
                {
                    _ = reader.Read();
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.Number:
                        {
                            var v = reader.GetUInt64();
                            stationEui = new StationEui(v);
                            break;
                        }
                        case JsonTokenType.String:
                        {
                            var s = reader.GetString();
                            stationEui = s.Contains(':', StringComparison.Ordinal)
                                       ? Id6.TryParse(s, out var id6) ? new StationEui(id6) : throw new JsonException()
                                       : Hexadecimal.TryParse(s, out var hhd, '-') ? new StationEui(hhd) : throw new JsonException();
                            break;
                        }
                        default:
                            throw new NotSupportedException();
                    }
                    readRouter = true;
                    _ = reader.Read();
                }
                else
                {
                    _ = reader.Read();
                    reader.Skip();
                    _ = reader.Read();
                }
            }

            if (!readRouter)
                throw new JsonException();
        }

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
