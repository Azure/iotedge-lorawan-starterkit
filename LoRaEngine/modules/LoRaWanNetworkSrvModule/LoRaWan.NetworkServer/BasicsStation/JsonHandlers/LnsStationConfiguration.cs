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
    using LoRaTools;
    using LoRaTools.Regions;

    internal static class LnsStationConfiguration
    {
        private enum Radio { Zero, One }

        private static T Map<T>(this Radio radio, T zero, T one) => radio switch
        {
            Radio.Zero => zero,
            Radio.One => one,
            _ => throw new ArgumentException(null, nameof(radio))
        };

        private class ChannelConfig
        {
            public ChannelConfig(bool enable, Radio radio, int @if)
            {
                Enable = enable;
                Radio = radio;
                If = @if;
            }

            public bool Enable { get; }
            public Radio Radio { get; }
            public int If { get; }
        }

        private class StandardConfig
        {
            public StandardConfig(bool enable, Radio radio, int @if, Bandwidth bandwidth, SpreadingFactor spreadingFactor)
            {
                Enable = enable;
                Radio = radio;
                If = @if;
                Bandwidth = bandwidth;
                SpreadingFactor = spreadingFactor;
            }

            public bool Enable { get; }
            public Radio Radio { get; }
            public int If { get; }
            public Bandwidth Bandwidth { get; }
            public SpreadingFactor SpreadingFactor { get; }
        }

        private class RadioConfig
        {
            public RadioConfig(bool enable, Hertz freq)
            {
                Enable = enable;
                Freq = freq;
            }

            public bool Enable { get; }
            public Hertz Freq { get; }
        }

        private class Sx1301Config
        {
            public Sx1301Config(RadioConfig radio0,
                                RadioConfig radio1,
                                StandardConfig channelLoraStd,
                                ChannelConfig channelFsk,
                                ChannelConfig channelMultiSf0,
                                ChannelConfig channelMultiSf1,
                                ChannelConfig channelMultiSf2,
                                ChannelConfig channelMultiSf3,
                                ChannelConfig channelMultiSf4,
                                ChannelConfig channelMultiSf5,
                                ChannelConfig channelMultiSf6,
                                ChannelConfig channelMultiSf7)
            {
                Radio0 = radio0;
                Radio1 = radio1;
                ChannelLoraStd = channelLoraStd;
                ChannelFsk = channelFsk;
                ChannelMultiSf0 = channelMultiSf0;
                ChannelMultiSf1 = channelMultiSf1;
                ChannelMultiSf2 = channelMultiSf2;
                ChannelMultiSf3 = channelMultiSf3;
                ChannelMultiSf4 = channelMultiSf4;
                ChannelMultiSf5 = channelMultiSf5;
                ChannelMultiSf6 = channelMultiSf6;
                ChannelMultiSf7 = channelMultiSf7;
            }

            public RadioConfig Radio0 { get; }
            public RadioConfig Radio1 { get; }
            public StandardConfig ChannelLoraStd { get; }
            public ChannelConfig ChannelFsk { get; }
            public ChannelConfig ChannelMultiSf0 { get; }
            public ChannelConfig ChannelMultiSf1 { get; }
            public ChannelConfig ChannelMultiSf2 { get; }
            public ChannelConfig ChannelMultiSf3 { get; }
            public ChannelConfig ChannelMultiSf4 { get; }
            public ChannelConfig ChannelMultiSf5 { get; }
            public ChannelConfig ChannelMultiSf6 { get; }
            public ChannelConfig ChannelMultiSf7 { get; }
        }

        private static readonly IJsonProperty<Radio> RadioProperty =
            JsonReader.Property("radio",
                                from r in JsonReader.Int32()
                                select r switch
                                {
                                    0 => Radio.Zero,
                                    1 => Radio.One,
                                    var n => throw new JsonException($"Invalid value for radio: {n}"),
                                });

        private static readonly IJsonReader<ChannelConfig> ChannelConfigReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              RadioProperty,
                              JsonReader.Property("if", JsonReader.Int32()),
                              (e, r, i) => new ChannelConfig(e, r, i));

        private static readonly IJsonReader<StandardConfig> StandardConfigReader =
            JsonReader.Object(JsonReader.Property("enable", JsonReader.Boolean()),
                              RadioProperty,
                              JsonReader.Property("if", JsonReader.Int32()),
                              JsonReader.Property("bandwidth", JsonReader.UInt32().AsEnum(n => unchecked((Bandwidth)(n / 1000 /* kHz */)))),
                              JsonReader.Property("spread_factor", JsonReader.UInt32().AsEnum(n => unchecked((SpreadingFactor)n))),
                              (e, r, i, bw, sf) => new StandardConfig(e, r, i, bw, sf));

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

        /// <summary>
        /// In the router configuration, a spreading factor of 0 is used for FSK.
        /// </summary>
        private const SpreadingFactor FskSpreadingFactor = 0;

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
                              JsonReader.Property("DRs",
                                  JsonReader.Array(from e in JsonReader.Tuple(JsonReader.Either(from n in JsonReader.UInt32().Validate(n => n == 0)
                                                                                                select FskSpreadingFactor,
                                                                                                JsonReader.UInt32().AsEnum(n => unchecked((SpreadingFactor)n))),
                                                                              from n in JsonReader.UInt32()
                                                                              select unchecked((Bandwidth)n),
                                                                              from n in JsonReader.UInt32()
                                                                              select n > 0)
                                                   select (SpreadingFactor: e.Item1, Bandwidth: e.Item2, DownloadOnly: e.Item3)
                                                   into e
                                                   select e.SpreadingFactor is FskSpreadingFactor ? (FskSpreadingFactor, 0, e.DownloadOnly)
                                                        : Enum.IsDefined(e.Bandwidth) ? (e.SpreadingFactor, e.Bandwidth, e.DownloadOnly)
                                                        : throw new JsonException($"Invalid bandwidth: {e.Bandwidth}"))),
                              JsonReader.Property("sx1301_conf", JsonReader.Array(Sx1301ConfReader)),
                              JsonReader.Property("nocca", JsonReader.Boolean()),
                              JsonReader.Property("nodc", JsonReader.Boolean()),
                              JsonReader.Property("nodwell", JsonReader.Boolean()),
                                JsonReader.Property("bcning",
                                    JsonReader.Object(JsonReader.Property("DR", JsonReader.UInt32()),
                                                      JsonReader.Property("layout", JsonReader.Array(JsonReader.UInt32())),
                                                      JsonReader.Property("freqs", JsonReader.Array(JsonReader.UInt32())), (dRs, layout, freqs) => new Beaconing(dRs, layout, freqs))),
                              (netId, joinEui, region, hwspec, freqRange, drs, sx1301conf, nocca, nodc, nodwell, bcning) =>
                                    WriteRouterConfig(netId, joinEui, region, hwspec, freqRange, drs,
                                                      sx1301conf, nocca, nodc, nodwell, bcning));

        private static readonly IJsonReader<Region> RegionConfigurationConverter =
            JsonReader.Object(JsonReader.Property("region", from s in JsonReader.String()
                                                            select Enum.TryParse<LoRaRegionType>(s, out var loraRegionType)
                                                                    ? RegionManager.TryTranslateToRegion(loraRegionType, out var resolvedRegion)
                                                                        ? resolvedRegion
                                                                        : throw new NotSupportedException($"'{loraRegionType}' is not a supported region.")
                                                                    : throw new JsonException($"'{s}' is not a valid region value as defined in '{nameof(LoRaRegionType)}'.")),
                              JsonReader.Property("sx1301_conf", JsonReader.Array(Sx1301ConfReader)),
                                  (region, radioConf) =>
                                  {
                                      if (region is RegionAS923 as923 && radioConf.FirstOrDefault() is { } configuration)
                                      {
                                          var chan0CentralFreq = configuration.ChannelMultiSf0.Radio.Map(configuration.Radio0.Freq,
                                                                                                         configuration.Radio1.Freq);
                                          var chan0Freq = chan0CentralFreq + configuration.ChannelMultiSf0.If;
                                          var chan1CentralFreq = configuration.ChannelMultiSf1.Radio.Map(configuration.Radio0.Freq,
                                                                                                         configuration.Radio1.Freq);
                                          var chan1Freq = chan1CentralFreq + configuration.ChannelMultiSf1.If;
                                          return as923.WithFrequencyOffset(chan0Freq, chan1Freq);
                                      }
                                      return region;
                                  });

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

        public static Region GetRegion(string jsonInput) => RegionConfigurationConverter.Read(jsonInput);

        private static string WriteRouterConfig(IEnumerable<NetId> allowedNetIds,
                                                IEnumerable<(JoinEui Min, JoinEui Max)> joinEuiRanges,
                                                string region,
                                                string hwspec,
                                                (Hertz Min, Hertz Max) freqRange,
                                                IEnumerable<(SpreadingFactor SpreadingFactor, Bandwidth Bandwidth, bool DnOnly)> dataRates,
                                                Sx1301Config[] sx1301Config,
                                                bool nocca, bool nodc, bool nodwell, Beaconing bcing)
        {
            if (string.IsNullOrEmpty(region)) throw new JsonException("Region must not be null.");
            if (string.IsNullOrEmpty(hwspec)) throw new JsonException("hwspec must not be null.");
            if (freqRange is var (minFreq, maxFreq) && minFreq == maxFreq) throw new JsonException("Minimum and maximum frequencies must differ.");
            if (dataRates.Count() is 0) throw new JsonException("Datarates list must not be empty.");
            if (sx1301Config.Length == 0) throw new JsonException("sx1301_conf must not be empty.");

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            writer.WriteString("msgtype", LnsMessageType.RouterConfig.ToBasicStationString());

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
                WriteChannelConfig("chan_FSK", config.ChannelFsk);
                WriteStandardConfig("chan_Lora_std", config.ChannelLoraStd);
                WriteChannelConfig("chan_multiSF_0", config.ChannelMultiSf0);
                WriteChannelConfig("chan_multiSF_1", config.ChannelMultiSf1);
                WriteChannelConfig("chan_multiSF_2", config.ChannelMultiSf2);
                WriteChannelConfig("chan_multiSF_3", config.ChannelMultiSf3);
                WriteChannelConfig("chan_multiSF_4", config.ChannelMultiSf4);
                WriteChannelConfig("chan_multiSF_5", config.ChannelMultiSf5);
                WriteChannelConfig("chan_multiSF_6", config.ChannelMultiSf6);
                WriteChannelConfig("chan_multiSF_7", config.ChannelMultiSf7);
                writer.WriteEndObject();
            }

            writer.WriteEndArray(); // sx1301_conf: [...]

            writer.WriteBoolean("nocca", nocca);
            writer.WriteBoolean("nodc", nodc);
            writer.WriteBoolean("nodwell", nodwell);

            writer.WritePropertyName("bcning");

            writer.WriteStartObject();
            writer.WriteNumber("DR", bcing.DR);
            writer.WritePropertyName("layout");
            writer.WriteStartArray();
            foreach (var layout in bcing.layout)
            {
                writer.WriteNumberValue(layout);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("freqs");

            writer.WriteStartArray();
            foreach (var freq in bcing.freqs)
            {
                writer.WriteNumberValue(freq);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();

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
                writer.WriteNumber("radio", (int)sxConf.Radio);
                writer.WriteNumber("if", sxConf.If);
                writer.WriteEndObject();
            }

            void WriteStandardConfig(string property, StandardConfig chanConf)
            {
                writer.WriteStartObject(property);
                writer.WriteBoolean("enable", chanConf.Enable);
                writer.WriteNumber("radio", (int)chanConf.Radio);
                writer.WriteNumber("if", chanConf.If);
                if (Enum.IsDefined(chanConf.Bandwidth))
                    writer.WriteNumber("bandwidth", chanConf.Bandwidth.ToHertz().AsUInt64);
                if (Enum.IsDefined(chanConf.SpreadingFactor))
                    writer.WriteNumber("spread_factor", (int)chanConf.SpreadingFactor);
                writer.WriteEndObject();
            }
        }
    }
}
