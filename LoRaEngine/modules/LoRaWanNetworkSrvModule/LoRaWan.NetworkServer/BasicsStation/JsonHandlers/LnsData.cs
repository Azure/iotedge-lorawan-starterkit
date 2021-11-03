// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Linq;
    using System.Text.Json;

    public static class LnsData
    {
        internal static readonly IJsonReader<LnsMessageType> MessageTypeReader =
            JsonReader.Object(
                JsonReader.Property("msgtype",
                    from s in JsonReader.String()
                    select s switch
                    {
                        "version"       => LnsMessageType.Version,
                        "router_config" => LnsMessageType.RouterConfig,
                        "jreq"          => LnsMessageType.JoinRequest,
                        "updf"          => LnsMessageType.UplinkDataFrame,
                        "dntxed"        => LnsMessageType.TransmitConfirmation,
                        "dnmsg"         => LnsMessageType.DownlinkMessage,
                        var type => throw new JsonException("Invalid or unsupported message type: " + type)
                    }));

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

        private static class RadioMetadataProperties
        {
            public static readonly IJsonProperty<DataRate> DataRate =
                JsonReader.Property("DR", from n in JsonReader.Byte() select new DataRate(n));

            public static readonly IJsonProperty<Hertz> Freq =
                JsonReader.Property("Freq", from n in JsonReader.UInt32() select new Hertz(n));

            public static readonly IJsonProperty<RadioMetadataUpInfo> UpInfo =
                JsonReader.Property("upinfo",
                                    JsonReader.Object(JsonReader.Property("rctx", JsonReader.UInt32()),
                                                      JsonReader.Property("xtime", JsonReader.UInt64()),
                                                      JsonReader.Property("gpstime", JsonReader.UInt32()),
                                                      JsonReader.Property("rssi", JsonReader.Double()),
                                                      JsonReader.Property("snr", JsonReader.Single()),
                                                      (Rctx, Xtime, GpsTime, Rssi, Snr) => new RadioMetadataUpInfo(Rctx, Xtime, GpsTime, Rssi, Snr)));
        }

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

        private static IJsonProperty<LnsMessageType> MessageTypeProperty(LnsMessageType expectedType) =>
            JsonReader.Property("msgtype",
                                from t in MessageTypeReader
                                select t == expectedType ? t : throw new JsonException("Invalid or unsupported message type: " + t));

        internal static readonly IJsonReader<UpstreamDataFrame> UpstreamDataFrameReader =
            JsonReader.Object(MessageTypeProperty(LnsMessageType.UplinkDataFrame),
                              JsonReader.Property("MHdr", JsonReader.Byte()),
                              JsonReader.Property("DevAddr", JsonReader.UInt32()),
                              JsonReader.Property("FCtrl", JsonReader.Byte()),
                              JsonReader.Property("FCnt", JsonReader.UInt16()),
                              JsonReader.Property("FOpts", JsonReader.String()),
                              JsonReader.Property("FPort", JsonReader.Byte()),
                              JsonReader.Property("FRMPayload", JsonReader.String()),
                              JsonReader.Property("MIC", JsonReader.Int32()),
                              RadioMetadataProperties.DataRate,
                              RadioMetadataProperties.Freq,
                              RadioMetadataProperties.UpInfo,
                              (_, MHdr, DevAddr, FCtrl, FCnt, FOpts, FPort, FRMPayload, MIC, dr, freq, upInfo) =>
                                new UpstreamDataFrame(new MacHeader(MHdr), new DevAddr(DevAddr), new FrameControl(FCtrl), FCnt, FOpts, new FramePort(FPort), FRMPayload, new Mic((uint)MIC),
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

        private static IJsonProperty<T> EuiProperty<T>(string name, Func<ulong, T> factory) =>
            JsonReader.Property(name,
                from s in JsonReader.String()
                select Hexadecimal.TryParse(s, out var eui, '-')
                     ? factory(eui)
                     : throw new JsonException($"Could not parse {name} as {typeof(T)}."));

        internal static readonly IJsonReader<JoinRequestFrame> JoinRequestFrameReader =
            JsonReader.Object(MessageTypeProperty(LnsMessageType.JoinRequest),
                              JsonReader.Property("MHdr", JsonReader.Byte()),
                              EuiProperty("JoinEui", eui => new JoinEui(eui)),
                              EuiProperty("DevEui", eui => new DevEui(eui)),
                              JsonReader.Property("DevNonce", JsonReader.UInt16()),
                              JsonReader.Property("MIC", JsonReader.Int32()),
                              RadioMetadataProperties.DataRate,
                              RadioMetadataProperties.Freq,
                              RadioMetadataProperties.UpInfo,
                              (_, MHdr, JoinEuiLong, DevEuiLong, DevNonce, MIC, dr, freq, upInfo) =>
                                new JoinRequestFrame(new MacHeader(MHdr), JoinEuiLong, DevEuiLong, new DevNonce(DevNonce), new Mic((uint)MIC),
                                                     new RadioMetadata(dr, freq, upInfo)));

    }
}
