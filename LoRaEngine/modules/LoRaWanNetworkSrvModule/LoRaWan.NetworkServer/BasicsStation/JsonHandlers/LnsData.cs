// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
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
        internal static readonly IJsonReader<RadioMetadata> RadioMetadataReader =
            JsonReader.Object(JsonReader.Property("DR", JsonReader.UInt32()),
                              JsonReader.Property("Freq", JsonReader.UInt32()),
                              JsonReader.Property("upinfo",
                                                  JsonReader.Object(JsonReader.Property("rctx", JsonReader.UInt32()),
                                                                    JsonReader.Property("xtime", JsonReader.UInt64()),
                                                                    JsonReader.Property("gpstime", JsonReader.UInt32()),
                                                                    JsonReader.Property("rssi", JsonReader.Double()),
                                                                    JsonReader.Property("snr", JsonReader.Single()),
                                                                    (Rctx, Xtime, GpsTime, Rssi, Snr) => (Rctx, Xtime, GpsTime, Rssi, Snr))),
                              (DR, Freq, upinfo) => new RadioMetadata(new DataRate((int)DR), new Hertz(Freq), upinfo.Rctx, upinfo.Xtime, upinfo.GpsTime, upinfo.Rssi, upinfo.Snr));

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
            JsonReader.Object(JsonReader.Property("msgtype", from s in JsonReader.String()
                                                             select s == "updf" ? LnsMessageType.UplinkDataFrame : throw new JsonException("Invalid or unsupported message type: " + s)),
                              JsonReader.Property("MHdr", JsonReader.Byte()),
                              JsonReader.Property("DevAddr", JsonReader.UInt32()),
                              JsonReader.Property("FCtrl", JsonReader.Byte()),
                              JsonReader.Property("FCnt", JsonReader.UInt16()),
                              JsonReader.Property("FOpts", JsonReader.String()),
                              JsonReader.Property("FPort", JsonReader.Byte()),
                              JsonReader.Property("FRMPayload", JsonReader.String()),
                              JsonReader.Property("MIC", JsonReader.Int32()),
                              (_, MHdr, DevAddr, FCtrl, FCnt, FOpts, FPort, FRMPayload, MIC) =>
                                new UpstreamDataFrame(new MacHeader(MHdr), new DevAddr(DevAddr), new FrameControl(FCtrl), FCnt, FOpts, new FramePort(FPort), FRMPayload, new Mic((uint)MIC)));
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
        internal static readonly IJsonReader<JoinRequestFrame> JoinRequestFrameReader =
            JsonReader.Object(JsonReader.Property("msgtype", from s in JsonReader.String()
                                                             select s == "jreq" ? LnsMessageType.UplinkDataFrame : throw new JsonException("Invalid or unsupported message type: " + s)),
                              JsonReader.Property("MHdr", JsonReader.Byte()),
                              JsonReader.Property("JoinEui", from s in JsonReader.String()
                                                             select Hexadecimal.TryParse(s, out var joinEuiLong, '-') ? joinEuiLong : throw new JsonException("Could not parse JoinEui.")),
                              JsonReader.Property("DevEui", from s in JsonReader.String()
                                                            select Hexadecimal.TryParse(s, out var devEuiLong, '-') ? devEuiLong : throw new JsonException("Could not parse JoinEui.")),
                              JsonReader.Property("DevNonce", JsonReader.UInt16()),
                              JsonReader.Property("MIC", JsonReader.Int32()),
                              (_, MHdr, JoinEuiLong, DevEuiLong, DevNonce, MIC) =>
                                new JoinRequestFrame(new MacHeader(MHdr), new JoinEui(JoinEuiLong), new DevEui(DevEuiLong), new DevNonce(DevNonce), new Mic((uint)MIC)));

    }
}
