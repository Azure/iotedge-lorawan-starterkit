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
            if (freqRange is var (minFreq, maxFreq) && minFreq == maxFreq) throw new ArgumentException("Expecting a range, therefore minimum and maximum frequencies should differ.");
            if (dataRates is null) throw new ArgumentNullException(nameof(dataRates));
            if (dataRates.Count() is 0) throw new ArgumentException($"Datarates list should not be empty.", nameof(dataRates));

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            writer.WriteString("msgtype", "router_config");

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

            writer.WritePropertyName("JoinEui");
            writer.WriteStartArray();
            if (joinEuiRanges is not null)
            {
                foreach (var (minJoinEui, maxJoinEui) in joinEuiRanges)
                {
                    writer.WriteStartArray();
                    writer.WriteNumberValue(minJoinEui.AsUInt64);
                    writer.WriteNumberValue(maxJoinEui.AsUInt64);
                    writer.WriteEndArray();
                }
            }
            writer.WriteEndArray();

            writer.WriteString("region", region);
            writer.WriteString("hwspec", hwspec);

            writer.WritePropertyName("freq_range");
            writer.WriteStartArray();
            writer.WriteNumberValue(freqRange.Min.AsUInt64);
            writer.WriteNumberValue(freqRange.Max.AsUInt64);
            writer.WriteEndArray();

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

            writer.WritePropertyName("sx1301_conf");
            writer.WriteStartArray();

            writer.WriteStartObject();
            WriteRadio("radio_0", enable: true, new Hertz(867500000));
            WriteRadio("radio_1", enable: true, new Hertz(868500000));
            WriteChan("chan_FSK", enable: true, radio: 1, @if: 300000);
            WriteChan("chan_Lora_std", enable: true, radio: 1, @if: -200000, (Bandwidth.BW250, SpreadingFactor.SF7));
            WriteChan("chan_multiSF_0", enable: true, radio: 1, @if: -400000);
            WriteChan("chan_multiSF_1", enable: true, radio: 1, @if: -200000);
            WriteChan("chan_multiSF_2", enable: true, radio: 1, @if: 0);
            WriteChan("chan_multiSF_3", enable: true, radio: 0, @if: -400000);
            WriteChan("chan_multiSF_4", enable: true, radio: 0, @if: -200000);
            WriteChan("chan_multiSF_5", enable: true, radio: 0, @if: 0);
            WriteChan("chan_multiSF_6", enable: true, radio: 0, @if: 200000);
            WriteChan("chan_multiSF_7", enable: true, radio: 0, @if: 400000);
            writer.WriteEndObject();

            writer.WriteEndArray(); // sx1301_conf: [...]

            writer.WriteBoolean("nocca", nocca);
            writer.WriteBoolean("nodc", nodc);
            writer.WriteBoolean("nodwell", nodwell);

            writer.WriteEndObject();

            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());

            void WriteRadio(string property, bool enable, Hertz frequency)
            {
                writer.WriteStartObject(property);
                writer.WriteBoolean("enable", enable);
                writer.WriteNumber("freq", frequency.AsUInt64);
                writer.WriteEndObject();
            }

            void WriteChan(string property, bool enable, int radio, int @if,
                (Bandwidth, SpreadingFactor) bwsf = default)
            {
                writer.WriteStartObject(property);
                writer.WriteBoolean("enable", enable);
                writer.WriteNumber("radio", radio);
                writer.WriteNumber("if", @if);
                var (bw, sf) = bwsf;
                if (bw != Bandwidth.Undefined)
                    writer.WriteNumber("bandwidth", bw.ToHertz().AsUInt64);
                if (sf != SpreadingFactor.Undefined)
                    writer.WriteNumber("spread_factor", (int)sf);
                writer.WriteEndObject();
            }
        }
    }
}
