// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Text.Json;

    public static class LnsData
    {
        internal static readonly IJsonReader<LnsMessageType> MessageTypeReader =
            JsonReader.Object(MessageTypeProperty());

        private static IJsonProperty<LnsMessageType> MessageTypeProperty(LnsMessageType? expectedType = null) =>
            JsonReader.Property("msgtype",
                                from t in JsonReader.String()
                                select LnsMessageTypeParser.ParseAndValidate(t, expectedType));

        /*
            {
                "msgtype"   : "version"
                "station"   : STRING
                "firmware"  : STRING
                "package"   : STRING
                "model"     : STRING
                "protocol"  : INT
                "features"  : STRING
            }
         */

        // We are deliberately ignoring firmware/package/model/protocol/features as these are not strictly needed at this stage of implementation
        // TODO Tests for this method are missing (waiting for more usefulness of it)

        internal static readonly IJsonReader<string> VersionMessageReader =
            JsonReader.Object(JsonReader.Property("station", JsonReader.String()));


        /*
                  {
                    ...
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

        internal static class RadioMetadataProperties
        {
            public static readonly IJsonProperty<DataRate> DataRate =
                JsonReader.Property("DR", JsonReader.Byte().Enum(v => (DataRate)v));

            public static readonly IJsonProperty<Hertz> Freq =
                JsonReader.Property("Freq", from n in JsonReader.UInt32() select new Hertz(n));

            public static readonly IJsonProperty<RadioMetadataUpInfo> UpInfo =
                JsonReader.Property("upinfo",
                                    JsonReader.Object(JsonReader.Property("rctx", JsonReader.UInt32()),
                                                      JsonReader.Property("xtime", JsonReader.UInt64()),
                                                      JsonReader.Property("gpstime", JsonReader.UInt32()),
                                                      JsonReader.Property("rssi", JsonReader.Double()),
                                                      JsonReader.Property("snr", JsonReader.Single()),
                                                      (rctx, xtime, gpsTime, rssi, snr) => new RadioMetadataUpInfo(rctx, xtime, gpsTime, rssi, snr)));
        }

        private static readonly IJsonProperty<Mic> MicProperty =
            JsonReader.Property("MIC", from i in JsonReader.Int32()
                                       select new Mic(unchecked((uint)i)));

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
              RADIOMETADATA
            }
         */

        internal static readonly IJsonReader<UpstreamDataFrame> UpstreamDataFrameReader =
            JsonReader.Object(MessageTypeProperty(LnsMessageType.UplinkDataFrame),
                              JsonReader.Property("MHdr", JsonReader.Byte()),
                              JsonReader.Property("DevAddr", JsonReader.UInt32()),
                              JsonReader.Property("FCtrl", from b in JsonReader.Byte() select FrameControl.Decode(b).Flags),
                              JsonReader.Property("FCnt", JsonReader.UInt16()),
                              JsonReader.Property("FOpts", JsonReader.String()),
                              JsonReader.Property("FPort", JsonReader.Byte()),
                              JsonReader.Property("FRMPayload", JsonReader.String()),
                              MicProperty,
                              RadioMetadataProperties.DataRate,
                              RadioMetadataProperties.Freq,
                              RadioMetadataProperties.UpInfo,
                              (_, mhdr, devAddr, fctrlFlags, cnt, opts, port, payload, mic, dr, freq, upInfo) =>
                                new UpstreamDataFrame(new MacHeader(mhdr), new DevAddr(devAddr), fctrlFlags, cnt, opts, new FramePort(port), payload, mic,
                                                      new RadioMetadata(dr, freq, upInfo)));
        /*
         * {
              "msgtype" : "jreq",
              "MHdr"    : UINT,
              "JoinEui" : EUI,
              "DevEui"  : EUI,
              "DevNonce": UINT,
              "MIC"     : INT32,
              ..
              RADIOMETADATA
            }
         */

        private static IJsonProperty<T> EuiProperty<T>(string name, Func<ulong, T> factory, char separator = '-') =>
            JsonReader.Property(name,
                                from s in JsonReader.String()
                                select Hexadecimal.TryParse(s, out ulong eui, separator)
                                     ? factory(eui)
                                     : throw new JsonException($"Could not parse {name} as {typeof(T)}."));

        internal static readonly IJsonReader<JoinRequestFrame> JoinRequestFrameReader =
            JsonReader.Object(MessageTypeProperty(LnsMessageType.JoinRequest),
                              JsonReader.Property("MHdr", JsonReader.Byte()),
                              EuiProperty("JoinEui", eui => new JoinEui(eui)),
                              EuiProperty("DevEui", eui => new DevEui(eui)),
                              JsonReader.Property("DevNonce", JsonReader.UInt16()),
                              MicProperty,
                              RadioMetadataProperties.DataRate,
                              RadioMetadataProperties.Freq,
                              RadioMetadataProperties.UpInfo,
                              (_, mhdr, joinEui, devEui, devNonce, mic, dr, freq, upInfo) =>
                                new JoinRequestFrame(new MacHeader(mhdr), joinEui, devEui, new DevNonce(devNonce), mic,
                                                     new RadioMetadata(dr, freq, upInfo)));

    }
}
