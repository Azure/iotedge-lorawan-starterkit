namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
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
              "msgtype"    : "router_config"
              "NetID"      : [ INT, .. ]
              "JoinEui"    : [ [INT,INT], .. ]  // ranges: beg,end inclusive
              "region"     : STRING             // e.g. "EU863", "US902", ..
              "hwspec"     : STRING
              "freq_range" : [ INT, INT ]       // min, max (hz)
              "DRs"        : [ [INT,INT,INT], .. ]   // sf,bw,dnonly
              "sx1301_conf": [ SX1301CONF, .. ]
              "nocca"      : BOOL
              "nodc"       : BOOL
              "nodwell"    : BOOL
            }
         */
        internal static string WriteRouterConfig(IEnumerable<NetId> allowedNetIds,
                                                 IEnumerable<(JoinEui Min, JoinEui Max)> joinEuiRanges,
                                                 string region,
                                                 string hwspec,
                                                 (Hertz Min, Hertz Max) freqRange,
                                                 IEnumerable<(SpreadingFactor SpreadingFactor, Bandwidth Bandwidth, bool DnOnly)> dataRates,
                                                 bool nocca = false,
                                                 bool nodc = false,
                                                 bool nodwell = false)
        {
            if (region is null) throw new ArgumentNullException(nameof(region));
            if (region.Length == 0) throw new ArgumentException("Region should not be empty.", nameof(region));
            if (hwspec is null) throw new ArgumentNullException(nameof(hwspec));
            if (hwspec.Length == 0) throw new ArgumentException("hwspec should not be empty.", nameof(hwspec));
            if (freqRange is var (min, max) && min.Equals(max)) throw new ArgumentException("Expecting a range, therefore minimum and maximum frequencies should differ.");
            if (dataRates is null) throw new ArgumentNullException(nameof(dataRates));
            if (dataRates.Count() is 0) throw new ArgumentException($"Datarates list should not be empty.", nameof(dataRates));

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            #region msgtype
            writer.WriteString("msgtype", "router_config");
            #endregion
            #region NetID
            writer.WritePropertyName("NetID");
            writer.WriteStartArray();
            if (allowedNetIds is not null)
            {
                foreach (var netId in allowedNetIds)
                {
                    writer.WriteNumberValue(netId.NetworkId);
                }
            }
            writer.WriteEndArray();
            #endregion
            #region JoinEui
            writer.WritePropertyName("JoinEui");
            writer.WriteStartArray();
            if (joinEuiRanges is not null)
            {
                foreach (var (Min, Max) in joinEuiRanges)
                {
                    writer.WriteStartArray();
                    writer.WriteNumberValue(Min.AsUInt64);
                    writer.WriteNumberValue(Max.AsUInt64);
                    writer.WriteEndArray();
                }
            }
            writer.WriteEndArray();
            #endregion
            #region region
            writer.WriteString("region", region);
            #endregion
            #region hwspec
            writer.WriteString("hwspec", hwspec);
            #endregion
            #region freq_range
            writer.WritePropertyName("freq_range");
            writer.WriteStartArray();
            writer.WriteNumberValue(freqRange.Min.AsUInt64);
            writer.WriteNumberValue(freqRange.Max.AsUInt64);
            writer.WriteEndArray();
            #endregion
            #region DRs
            writer.WritePropertyName("DRs");
            writer.WriteStartArray();
            foreach (var (sf, bw, dnOnly) in dataRates)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue((int)sf);
                writer.WriteNumberValue((int)bw);
                writer.WriteNumberValue(dnOnly ? 1 : 0);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            #endregion
            #region sx1301_conf
            writer.WritePropertyName("sx1301_conf");
            writer.WriteStartArray();

            writer.WriteStartObject();
            #region radio_0
            writer.WriteStartObject("radio_0");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("freq", 867500000);
            writer.WriteEndObject();
            #endregion
            #region radio_1
            writer.WriteStartObject("radio_1");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("freq", 868500000);
            writer.WriteEndObject();
            #endregion
            #region chan_FSK
            writer.WriteStartObject("chan_FSK");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 1);
            writer.WriteNumber("if", 300000);
            writer.WriteEndObject();
            #endregion
            #region chan_Lora_std
            writer.WriteStartObject("chan_Lora_std");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 1);
            writer.WriteNumber("if", -200000);
            writer.WriteNumber("bandwidth", 250_000);
            writer.WriteNumber("spread_factor", 7);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_0
            writer.WriteStartObject("chan_multiSF_0");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 1);
            writer.WriteNumber("if", -400000);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_1
            writer.WriteStartObject("chan_multiSF_1");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 1);
            writer.WriteNumber("if", -200000);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_2
            writer.WriteStartObject("chan_multiSF_2");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 1);
            writer.WriteNumber("if", 0);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_3
            writer.WriteStartObject("chan_multiSF_3");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 0);
            writer.WriteNumber("if", -400000);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_4
            writer.WriteStartObject("chan_multiSF_4");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 0);
            writer.WriteNumber("if", -200000);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_5
            writer.WriteStartObject("chan_multiSF_5");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 0);
            writer.WriteNumber("if", 0);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_6
            writer.WriteStartObject("chan_multiSF_6");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 0);
            writer.WriteNumber("if", 200000);
            writer.WriteEndObject();
            #endregion
            #region chan_multiSF_7
            writer.WriteStartObject("chan_multiSF_7");
            writer.WriteBoolean("enable", true);
            writer.WriteNumber("radio", 0);
            writer.WriteNumber("if", 400000);
            writer.WriteEndObject();
            #endregion
            writer.WriteEndObject();

            writer.WriteEndArray();
            #endregion
            #region nocca
            writer.WriteBoolean("nocca", nocca);
            #endregion
            #region nodc
            writer.WriteBoolean("nodc", nodc);
            #endregion
            #region nodwell
            writer.WriteBoolean("nodwell", nodwell);
            #endregion

            writer.WriteEndObject();

            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
