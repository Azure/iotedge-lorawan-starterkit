// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    internal static class LnsStationConfiguration
    {
        private struct ChannelConfig
        {
            public bool Enable { get; set; }
            public bool Radio { get; set; }
            public int If { get; set; }
        }

        private struct StandardConfig
        {
            public bool Enable { get; set; }
            public int Radio { get; set; }
            public int If { get; set; }
            public Bandwidth Bandwidth { get; set; }
            public SpreadingFactor SpreadFactor { get; set; }
        }

        private struct RadioConfig
        {
            public bool Enable { get; set; }
            public Hertz Freq { get; set; }
        }

        private struct Sx1301Conf
        {
            public RadioConfig Radio0 { get; set; }
            public RadioConfig Radio1 { get; set; }
            public StandardConfig ChanLoraStd { get; set; }
            public ChannelConfig ChanFsk { get; set; }
            public ChannelConfig ChanMultiSf0 { get; set; }
            public ChannelConfig ChanMultiSf1 { get; set; }
            public ChannelConfig ChanMultiSf2 { get; set; }
            public ChannelConfig ChanMultiSf3 { get; set; }
            public ChannelConfig ChanMultiSf4 { get; set; }
            public ChannelConfig ChanMultiSf5 { get; set; }
            public ChannelConfig ChanMultiSf6 { get; set; }
            public ChannelConfig ChanMultiSf7 { get; set; }
        }

        private static readonly IJsonReader<ChannelConfig> ChanConfReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              JsonReader.Property("radio", JsonReader.Int32()),
                              JsonReader.Property("if", JsonReader.Int32()),
                              (e, r, i) => new ChannelConfig { Enable = e, Radio = r == 1, If = i });

        private static readonly IJsonReader<StandardConfig> StdConfReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              JsonReader.Property("radio", JsonReader.Int32()),
                              JsonReader.Property("if", JsonReader.Int32()),
                              JsonReader.Property("bandwidth", JsonReader.UInt32()),
                              JsonReader.Property("spread_factor", JsonReader.UInt32()),
                              (e, r, i, b, sf) => new StandardConfig { Enable = e, Radio = r, If = i, Bandwidth = (Bandwidth)(b / 1000), SpreadFactor = (SpreadingFactor)sf });

        private static readonly IJsonReader<RadioConfig> RadioConfReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              JsonReader.Property("freq", JsonReader.UInt32()),
                              (e, f) => new RadioConfig { Enable = e, Freq = new Hertz(f) });

        private static readonly IJsonReader<Sx1301Conf> Sx1301ConfReader =
            JsonReader.Object(JsonReader.Property("radio_0", RadioConfReader),
                              JsonReader.Property("radio_1", RadioConfReader),
                              JsonReader.Property("chan_Lora_std", StdConfReader),
                              JsonReader.Property("chan_FSK", ChanConfReader),
                              JsonReader.Property("chan_multiSF_0", ChanConfReader),
                              JsonReader.Property("chan_multiSF_1", ChanConfReader),
                              JsonReader.Property("chan_multiSF_2", ChanConfReader),
                              JsonReader.Property("chan_multiSF_3", ChanConfReader),
                              JsonReader.Property("chan_multiSF_4", ChanConfReader),
                              JsonReader.Property("chan_multiSF_5", ChanConfReader),
                              JsonReader.Property("chan_multiSF_6", ChanConfReader),
                              JsonReader.Property("chan_multiSF_7", ChanConfReader),
                              (r0, r1, std, fsk, sf0, sf1, sf2, sf3, sf4, sf5, sf6, sf7) => new Sx1301Conf
                              {
                                  Radio0 = r0,
                                  Radio1 = r1,
                                  ChanLoraStd = std,
                                  ChanFsk = fsk,
                                  ChanMultiSf0 = sf0,
                                  ChanMultiSf1 = sf1,
                                  ChanMultiSf2 = sf2,
                                  ChanMultiSf3 = sf3,
                                  ChanMultiSf4 = sf4,
                                  ChanMultiSf5 = sf5,
                                  ChanMultiSf6 = sf6,
                                  ChanMultiSf7 = sf7,
                              });

        private static readonly IJsonReader<string> RouterConfigurationConverter =
            JsonReader.Object(JsonReader.Property("NetID", JsonReader.Array(from id in JsonReader.UInt32()
                                                                            select new NetId((int)id))),
                              JsonReader.Property("JoinEui",
                                                  JsonReader.Either(JsonReader.Array(from arr in JsonReader.Array(from eui in JsonReader.String()
                                                                                                                  select JoinEui.Parse(eui))
                                                                                     select (arr[0], arr[1])),
                                                                    JsonReader.Null<(JoinEui, JoinEui)[]>()),
                                                  (true, Array.Empty<(JoinEui, JoinEui)>())),
                              JsonReader.Property("region", JsonReader.String()),
                              JsonReader.Property("hwspec", JsonReader.String()),
                              JsonReader.Property("freq_range", from r in JsonReader.Array(JsonReader.UInt32())
                                                                select (new Hertz(r[0]), new Hertz(r[1]))),
                              JsonReader.Property("DRs", JsonReader.Array(from arr in JsonReader.Array(JsonReader.UInt32())
                                                                          select ((SpreadingFactor)arr[0], (Bandwidth)arr[1], Convert.ToBoolean(arr[2])))),
                              JsonReader.Property("sx1301_conf", JsonReader.Array(Sx1301ConfReader)),
                              JsonReader.Property("nocca", JsonReader.Boolean()),
                              JsonReader.Property("nodc", JsonReader.Boolean()),
                              JsonReader.Property("nodwell", JsonReader.Boolean()),
                              (netId, joinEui, region, hwspec, freqRange, drs, sx1301conf, nocca, nodc, nodwell) =>
                                    WriteRouterConfig(netId, joinEui, region, hwspec, freqRange, drs,
                                                      sx1301conf, nocca: nocca, nodc: nodc, nodwell: nodwell));

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

        public static string GetConfiguration(string jsonInput) => RouterConfigurationConverter.Read(jsonInput);

        private static string WriteRouterConfig(IEnumerable<NetId> allowedNetIds,
                                                IEnumerable<(JoinEui Min, JoinEui Max)> joinEuiRanges,
                                                string region,
                                                string hwspec,
                                                (Hertz Min, Hertz Max) freqRange,
                                                IEnumerable<(SpreadingFactor SpreadingFactor, Bandwidth Bandwidth, bool DnOnly)> dataRates,
                                                Sx1301Conf[] sx1301conf,
                                                bool nocca, bool nodc, bool nodwell)
        {
            if (string.IsNullOrEmpty(region)) throw new JsonException("Region must not be null.");
            if (string.IsNullOrEmpty(hwspec)) throw new JsonException("hwspec must not be null.");
            if (freqRange is var (minFreq, maxFreq) && minFreq == maxFreq) throw new JsonException("Minimum and maximum frequencies must differ.");
            if (dataRates.Count() is 0) throw new JsonException("Datarates list must not be empty.");
            if (sx1301conf.Length == 0) throw new JsonException("sx1301_conf must not be empty.");

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

            foreach (var conf in sx1301conf)
            {
                writer.WriteStartObject();
                WriteRadioConfig("radio_0", conf.Radio0);
                WriteRadioConfig("radio_1", conf.Radio1);
                WriteChannelConfig("chan_FSK", conf.ChanFsk);
                WriteStandardConfig("chan_Lora_std", conf.ChanLoraStd);
                WriteChannelConfig("chan_multiSF_0", conf.ChanMultiSf0);
                WriteChannelConfig("chan_multiSF_1", conf.ChanMultiSf1);
                WriteChannelConfig("chan_multiSF_2", conf.ChanMultiSf2);
                WriteChannelConfig("chan_multiSF_3", conf.ChanMultiSf3);
                WriteChannelConfig("chan_multiSF_4", conf.ChanMultiSf4);
                WriteChannelConfig("chan_multiSF_5", conf.ChanMultiSf5);
                WriteChannelConfig("chan_multiSF_6", conf.ChanMultiSf6);
                WriteChannelConfig("chan_multiSF_7", conf.ChanMultiSf7);
                writer.WriteEndObject();
            }

            writer.WriteEndArray(); // sx1301_conf: [...]

            writer.WriteBoolean("nocca", nocca);
            writer.WriteBoolean("nodc", nodc);
            writer.WriteBoolean("nodwell", nodwell);

            writer.WriteEndObject();

            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());

            void WriteRadioConfig(string property, RadioConfig radioConf)
            {
                writer.WriteStartObject(property);
                writer.WriteBoolean("enable", radioConf.Enable);
                writer.WriteNumber("freq", radioConf.Freq.AsUInt64);
                writer.WriteEndObject();
            }

            void WriteChannelConfig(string property, ChannelConfig sxConf)
            {
                writer.WriteStartObject(property);
                writer.WriteBoolean("enable", sxConf.Enable);
                writer.WriteNumber("radio", sxConf.Radio ? 1 : 0);
                writer.WriteNumber("if", sxConf.If);
                writer.WriteEndObject();
            }

            void WriteStandardConfig(string property, StandardConfig chanConf)
            {
                writer.WriteStartObject(property);
                writer.WriteBoolean("enable", chanConf.Enable);
                writer.WriteNumber("radio", chanConf.Radio);
                writer.WriteNumber("if", chanConf.If);
                if (chanConf.Bandwidth != Bandwidth.Undefined)
                    writer.WriteNumber("bandwidth", chanConf.Bandwidth.ToHertz().AsUInt64);
                if (chanConf.SpreadFactor != SpreadingFactor.Undefined)
                    writer.WriteNumber("spread_factor", (int)chanConf.SpreadFactor);
                writer.WriteEndObject();
            }
        }
    }
}
