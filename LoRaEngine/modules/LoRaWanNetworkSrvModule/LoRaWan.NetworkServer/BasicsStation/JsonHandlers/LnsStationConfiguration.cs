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
            public ChannelConfig(bool enable, bool radio, int @if)
            {
                Enable = enable;
                Radio = radio;
                If = @if;
            }

            public bool Enable { get; }
            public bool Radio { get; }
            public int If { get; }
        }

        private struct StandardConfig
        {
            public StandardConfig(bool enable, bool radio, int @if, Bandwidth bandwidth, SpreadingFactor spreadingFactor)
            {
                Enable = enable;
                Radio = radio;
                If = @if;
                Bandwidth = bandwidth;
                SpreadingFactor = spreadingFactor;
            }

            public bool Enable { get; }
            public bool Radio { get; }
            public int If { get; }
            public Bandwidth Bandwidth { get; }
            public SpreadingFactor SpreadingFactor { get; }
        }

        private struct RadioConfig
        {
            public RadioConfig(bool enable, Hertz freq)
            {
                Enable = enable;
                Freq = freq;
            }

            public bool Enable { get; }
            public Hertz Freq { get; }
        }

        private struct Sx1301Config
        {
            public Sx1301Config(RadioConfig radio0,
                                RadioConfig radio1,
                                StandardConfig chanLoraStd,
                                ChannelConfig chanFsk,
                                ChannelConfig chanMultiSf0,
                                ChannelConfig chanMultiSf1,
                                ChannelConfig chanMultiSf2,
                                ChannelConfig chanMultiSf3,
                                ChannelConfig chanMultiSf4,
                                ChannelConfig chanMultiSf5,
                                ChannelConfig chanMultiSf6,
                                ChannelConfig chanMultiSf7)
            {
                Radio0 = radio0;
                Radio1 = radio1;
                ChanLoraStd = chanLoraStd;
                ChanFsk = chanFsk;
                ChanMultiSf0 = chanMultiSf0;
                ChanMultiSf1 = chanMultiSf1;
                ChanMultiSf2 = chanMultiSf2;
                ChanMultiSf3 = chanMultiSf3;
                ChanMultiSf4 = chanMultiSf4;
                ChanMultiSf5 = chanMultiSf5;
                ChanMultiSf6 = chanMultiSf6;
                ChanMultiSf7 = chanMultiSf7;
            }

            public RadioConfig Radio0 { get; }
            public RadioConfig Radio1 { get; }
            public StandardConfig ChanLoraStd { get; }
            public ChannelConfig ChanFsk { get; }
            public ChannelConfig ChanMultiSf0 { get; }
            public ChannelConfig ChanMultiSf1 { get; }
            public ChannelConfig ChanMultiSf2 { get; }
            public ChannelConfig ChanMultiSf3 { get; }
            public ChannelConfig ChanMultiSf4 { get; }
            public ChannelConfig ChanMultiSf5 { get; }
            public ChannelConfig ChanMultiSf6 { get; }
            public ChannelConfig ChanMultiSf7 { get; }
        }

        private static readonly IJsonReader<ChannelConfig> ChannelConfigReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              JsonReader.Property("radio", JsonReader.Int32()),
                              JsonReader.Property("if", JsonReader.Int32()),
                              (e, r, i) => new ChannelConfig(e, r == 1, i));

        private static readonly IJsonReader<StandardConfig> StandardConfigReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              JsonReader.Property("radio", JsonReader.Int32()),
                              JsonReader.Property("if", JsonReader.Int32()),
                              JsonReader.Property("bandwidth", JsonReader.UInt32()),
                              JsonReader.Property("spread_factor", JsonReader.UInt32()),
                              (e, r, i, b, sf) => new StandardConfig(e, r == 1, i, (Bandwidth)(b / 1000), (SpreadingFactor)sf));

        private static readonly IJsonReader<RadioConfig> RadioConfigReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              JsonReader.Property("freq", JsonReader.UInt32()),
                              (e, f) => new RadioConfig(e, new Hertz(f)));

        private static readonly IJsonReader<Sx1301Config> Sx1301ConfReader =
            JsonReader.Object(JsonReader.Property("radio_0", RadioConfigReader),
                              JsonReader.Property("radio_1", RadioConfigReader),
                              JsonReader.Property("chan_Lora_std", StandardConfigReader),
                              JsonReader.Property("chan_FSK", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_0", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_1", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_2", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_3", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_4", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_5", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_6", ChannelConfigReader),
                              JsonReader.Property("chan_multiSF_7", ChannelConfigReader),
                              (r0, r1, std, fsk, sf0, sf1, sf2, sf3, sf4, sf5, sf6, sf7) =>
                              new Sx1301Config(r0, r1, std, fsk, sf0, sf1, sf2, sf3, sf4, sf5, sf6, sf7));

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
                                                      sx1301conf, nocca, nodc, nodwell));

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
                                                Sx1301Config[] sx1301Config,
                                                bool nocca, bool nodc, bool nodwell)
        {
            if (string.IsNullOrEmpty(region)) throw new JsonException("Region must not be null.");
            if (string.IsNullOrEmpty(hwspec)) throw new JsonException("hwspec must not be null.");
            if (freqRange is var (minFreq, maxFreq) && minFreq == maxFreq) throw new JsonException("Minimum and maximum frequencies must differ.");
            if (dataRates.Count() is 0) throw new JsonException("Datarates list must not be empty.");
            if (sx1301Config.Length == 0) throw new JsonException("sx1301_conf must not be empty.");

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

            foreach (var config in sx1301Config)
            {
                writer.WriteStartObject();
                WriteRadioConfig("radio_0", config.Radio0);
                WriteRadioConfig("radio_1", config.Radio1);
                WriteChannelConfig("chan_FSK", config.ChanFsk);
                WriteStandardConfig("chan_Lora_std", config.ChanLoraStd);
                WriteChannelConfig("chan_multiSF_0", config.ChanMultiSf0);
                WriteChannelConfig("chan_multiSF_1", config.ChanMultiSf1);
                WriteChannelConfig("chan_multiSF_2", config.ChanMultiSf2);
                WriteChannelConfig("chan_multiSF_3", config.ChanMultiSf3);
                WriteChannelConfig("chan_multiSF_4", config.ChanMultiSf4);
                WriteChannelConfig("chan_multiSF_5", config.ChanMultiSf5);
                WriteChannelConfig("chan_multiSF_6", config.ChanMultiSf6);
                WriteChannelConfig("chan_multiSF_7", config.ChanMultiSf7);
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
                writer.WriteNumber("radio", chanConf.Radio ? 1 : 0);
                writer.WriteNumber("if", chanConf.If);
                if (chanConf.Bandwidth != Bandwidth.Undefined)
                    writer.WriteNumber("bandwidth", chanConf.Bandwidth.ToHertz().AsUInt64);
                if (chanConf.SpreadingFactor != SpreadingFactor.Undefined)
                    writer.WriteNumber("spread_factor", (int)chanConf.SpreadingFactor);
                writer.WriteEndObject();
            }
        }
    }
}
