// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static Bandwidth;
    using static SpreadingFactor;
    using global::LoRaTools.Regions;

    public class LnsStationConfigurationTests
    {
        internal static string ValidStationConfiguration =
            GetTwinConfigurationJson(new[] { new NetId(1) },
                                     new[] { (new JoinEui(ulong.MinValue), new JoinEui(ulong.MaxValue)) },
                                     "EU863",
                                     "sx1301/1",
                                     (new Hertz(863000000), new Hertz(870000000)),
                                     new[]
                                     {
                                         (SF11, BW125, false),
                                         (SF10, BW125, false),
                                         (SF9 , BW125, false),
                                         (SF8 , BW125, false),
                                         (SF7 , BW125, false),
                                         (SF7 , BW250, false),
                                     },
                                     flags: RouterConfigStationFlags.NoClearChannelAssessment | RouterConfigStationFlags.NoDutyCycle | RouterConfigStationFlags.NoDwellTimeLimitations);

        internal static string ValidRouterConfigMessage = JsonUtil.Strictify(@"{
            'msgtype': 'router_config',
            'NetID': [1],
            'JoinEui': [[0, 18446744073709551615]],
	        'region': 'EU863',
	        'hwspec': 'sx1301/1',
	        'freq_range': [ 863000000, 870000000 ],
            'DRs': [ [ 11, 125, 0 ],
                       [ 10, 125, 0 ],
                       [ 9, 125, 0 ],
                       [ 8, 125, 0 ],
                       [ 7, 125, 0 ],
                       [ 7, 250, 0 ] ],
            'sx1301_conf': [
                        {
                            'radio_0': {
                                'enable': true,
                                'freq': 867500000
                            },
                            'radio_1': {
                                'enable': true,
                                'freq': 868500000
                            },
                            'chan_FSK': {
                                'enable': true,
                                'radio': 1,
                                'if': 300000
                            },
                            'chan_Lora_std': {
                                'enable': true,
                                'radio': 1,
                                'if': -200000,
                                'bandwidth': 250000,
                                'spread_factor': 7
                            },
                            'chan_multiSF_0': {
                                'enable': true,
                                'radio': 1,
                                'if': -400000
                            },
                            'chan_multiSF_1': {
                                'enable': true,
                                'radio': 1,
                                'if': -200000
                            },
                            'chan_multiSF_2': {
                                'enable': true,
                                'radio': 1,
                                'if': 0
                            },
                            'chan_multiSF_3': {
                                'enable': true,
                                'radio': 0,
                                'if': -400000
                            },
                            'chan_multiSF_4': {
                                'enable': true,
                                'radio': 0,
                                'if': -200000
                            },
                            'chan_multiSF_5': {
                                'enable': true,
                                'radio': 0,
                                'if': 0
                            },
                            'chan_multiSF_6': {
                                'enable': true,
                                'radio': 0,
                                'if': 200000
                            },
                            'chan_multiSF_7': {
                                'enable': true,
                                'radio': 0,
                                'if': 400000
                            }
                        }
                    ],
            'nocca': true,
            'nodc': true,
            'nodwell': true}");

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void WriteRouterConfig_WithEmptyOrNullJoinEuiFilter(int? JoinEuiCount)
        {
            // arrange
            var expected = JsonUtil.Strictify(@"{
                    'msgtype': 'router_config',
                    'NetID': [1],
                    'JoinEui': [],
	                'region': 'EU863',
	                'hwspec': 'sx1301/1',
	                'freq_range': [ 863000000, 870000000 ],
                    'DRs': [ [ 11, 125, 0 ],
                               [ 10, 125, 0 ],
                               [ 9, 125, 0 ],
                               [ 8, 125, 0 ],
                               [ 7, 125, 0 ],
                               [ 7, 250, 0 ] ],
                    'sx1301_conf': [
                                {
                                    'radio_0': {
                                        'enable': true,
                                        'freq': 867500000
                                    },
                                    'radio_1': {
                                        'enable': true,
                                        'freq': 868500000
                                    },
                                    'chan_FSK': {
                                        'enable': true,
                                        'radio': 1,
                                        'if': 300000
                                    },
                                    'chan_Lora_std': {
                                        'enable': true,
                                        'radio': 1,
                                        'if': -200000,
                                        'bandwidth': 250000,
                                        'spread_factor': 7
                                    },
                                    'chan_multiSF_0': {
                                        'enable': true,
                                        'radio': 1,
                                        'if': -400000
                                    },
                                    'chan_multiSF_1': {
                                        'enable': true,
                                        'radio': 1,
                                        'if': -200000
                                    },
                                    'chan_multiSF_2': {
                                        'enable': true,
                                        'radio': 1,
                                        'if': 0
                                    },
                                    'chan_multiSF_3': {
                                        'enable': true,
                                        'radio': 0,
                                        'if': -400000
                                    },
                                    'chan_multiSF_4': {
                                        'enable': true,
                                        'radio': 0,
                                        'if': -200000
                                    },
                                    'chan_multiSF_5': {
                                        'enable': true,
                                        'radio': 0,
                                        'if': 0
                                    },
                                    'chan_multiSF_6': {
                                        'enable': true,
                                        'radio': 0,
                                        'if': 200000
                                    },
                                    'chan_multiSF_7': {
                                        'enable': true,
                                        'radio': 0,
                                        'if': 400000
                                    }
                                }
                            ],
                    'nocca': true,
                    'nodc': true,
                    'nodwell': true}");

            var input = GetTwinConfigurationJson(new[] { new NetId(1) },
                                                 JoinEuiCount.HasValue ? Array.Empty<(JoinEui, JoinEui)>() : null,
                                                 "EU863",
                                                 "sx1301/1",
                                                 (new Hertz(863000000), new Hertz(870000000)),
                                                 new[]
                                                 {
                                                     (SF11, BW125, false),
                                                     (SF10, BW125, false),
                                                     (SF9 , BW125, false),
                                                     (SF8 , BW125, false),
                                                     (SF7 , BW125, false),
                                                     (SF7 , BW250, false),
                                                 },
                                                 flags: RouterConfigStationFlags.NoClearChannelAssessment | RouterConfigStationFlags.NoDutyCycle | RouterConfigStationFlags.NoDwellTimeLimitations);

            // act
            var actual = LnsStationConfiguration.GetConfiguration(input);

            // assert
            Assert.Equal(JsonUtil.Minify(expected), actual);
        }

        [Theory]
        [InlineData(null, "something")]
        [InlineData("something", null)]
        [InlineData("something", "")]
        [InlineData("", "something")]
        public void WriteRouterConfig_Throws_WhenRegionOrHwspecIsNullOrEmpty(string region, string hwspec)
        {
            // arrange
            var input = GetTwinConfigurationJson(Array.Empty<NetId>(),
                                                 Array.Empty<(JoinEui, JoinEui)>(),
                                                 region,
                                                 hwspec,
                                                 (new Hertz(863000000), new Hertz(870000000)),
                                                 new[] { (SF11, BW125, false) });

            // act + assert
            Assert.Throws<JsonException>(() => LnsStationConfiguration.GetConfiguration(input));
        }

        [Fact]
        public void WriteRouterConfig()
        {
            // act
            var actual = LnsStationConfiguration.GetConfiguration(ValidStationConfiguration);

            // assert
            Assert.Equal(JsonUtil.Minify(ValidRouterConfigMessage), actual);
        }

        [Fact]
        public void WriteRouterConfig_ThrowsArgumentException_WithInvalidFrequencyRange()
        {
            // arrange
            var input = GetTwinConfigurationJson(Array.Empty<NetId>(),
                                                 Array.Empty<(JoinEui, JoinEui)>(),
                                                 "region",
                                                 "hwspec",
                                                 (new Hertz(0), new Hertz(0)),
                                                 new[] { (SF11, BW125, false) });

            // act + assert
            Assert.Throws<JsonException>(() => LnsStationConfiguration.GetConfiguration(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void WriteRouterConfig_Throws_WithNullOrEmptyDataRates(int? dataRates)
        {
            // arrange
            var input = GetTwinConfigurationJson(Array.Empty<NetId>(),
                                                 Array.Empty<(JoinEui, JoinEui)>(),
                                                 "region",
                                                 "hwspec",
                                                 (new Hertz(863000000), new Hertz(870000000)),
                                                 dataRates.HasValue ? Array.Empty<(SpreadingFactor, Bandwidth, bool)>() : null);

            // act + assert
            Assert.Throws<JsonException>(() => LnsStationConfiguration.GetConfiguration(input));
        }

        [Theory]
        [InlineData(125_000)]
        public void WriteRouterConfig_Throws_WhenInvalidBandwidth(int bandwidth)
        {
            // arrange
            var input = GetTwinConfigurationJson(Array.Empty<NetId>(),
                                                 Array.Empty<(JoinEui, JoinEui)>(),
                                                 "region",
                                                 "hwspec",
                                                 (new Hertz(863000000), new Hertz(870000000)),
                                                 new[] { (SF11, (Bandwidth)bandwidth, false) });

            // act + assert
            Assert.Throws<JsonException>(() => LnsStationConfiguration.GetConfiguration(input));
        }

        [Fact]
        public void WriteRouterConfig_Throws_WhenInvalidSpreadFactor()
        {
            // arrange
            var input = GetTwinConfigurationJson(Array.Empty<NetId>(),
                                                 Array.Empty<(JoinEui, JoinEui)>(),
                                                 "region",
                                                 "hwspec",
                                                 (new Hertz(863000000), new Hertz(870000000)),
                                                 new[] { ((SpreadingFactor)42, BW250, false) });

            // act + assert
            Assert.Throws<JsonException>(() => LnsStationConfiguration.GetConfiguration(input));
        }

        [Theory]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData(@"[{ ""radio_0"": { ""enable"": true, ""freq"": 867500000 } }]")]
        public void WriteRouterConfig_Throws_WhenInvalidSx1301Conf(string sx1301Conf)
        {
            // arrange
            var input = GetTwinConfigurationJson(Array.Empty<NetId>(),
                                                 Array.Empty<(JoinEui, JoinEui)>(),
                                                 "region",
                                                 "hwspec",
                                                 (new Hertz(863000000), new Hertz(870000000)),
                                                 new[] { (SF11, BW125, false) },
                                                 sx1301Conf);

            // act + assert
            Assert.Throws<JsonException>(() => LnsStationConfiguration.GetConfiguration(input));
        }

        internal static string GetTwinConfigurationJson(IEnumerable<NetId> allowedNetIds,
                                                        IEnumerable<(JoinEui Min, JoinEui Max)> joinEuiRanges,
                                                        string region,
                                                        string hwspec,
                                                        (Hertz Min, Hertz Max) freqRange,
                                                        IEnumerable<(SpreadingFactor SpreadingFactor, Bandwidth Bandwidth, bool DnOnly)> dataRates,
                                                        string sx1301Conf = null,
                                                        RouterConfigStationFlags flags = RouterConfigStationFlags.None)
        {
            var defaultSx1301Conf = JsonUtil.Strictify(@"[{
                'radio_0': {
                    'enable': true,
                    'freq': 867500000
                },
                'radio_1': {
                    'enable': true,
                    'freq': 868500000
                },
                'chan_FSK': {
                    'enable': true,
                    'radio': 1,
                    'if': 300000
                },
                'chan_Lora_std': {
                    'enable': true,
                    'radio': 1,
                    'if': -200000,
                    'bandwidth': 250000,
                    'spread_factor': 7
                },
                'chan_multiSF_0': {
                    'enable': true,
                    'radio': 1,
                    'if': -400000
                },
                'chan_multiSF_1': {
                    'enable': true,
                    'radio': 1,
                    'if': -200000
                },
                'chan_multiSF_2': {
                    'enable': true,
                    'radio': 1,
                    'if': 0
                },
                'chan_multiSF_3': {
                    'enable': true,
                    'radio': 0,
                    'if': -400000
                },
                'chan_multiSF_4': {
                    'enable': true,
                    'radio': 0,
                    'if': -200000
                },
                'chan_multiSF_5': {
                    'enable': true,
                    'radio': 0,
                    'if': 0
                },
                'chan_multiSF_6': {
                    'enable': true,
                    'radio': 0,
                    'if': 200000
                },
                'chan_multiSF_7': {
                    'enable': true,
                    'radio': 0,
                    'if': 400000
                }
            }]");

            const string template = @"{{
                ""msgtype"": ""router_config"",
                ""NetID"": {0},
                ""JoinEui"": {1},
	            ""region"": {2},
	            ""hwspec"": {3},
	            ""freq_range"": {4},
                ""DRs"": {5},
                ""sx1301_conf"": {6},
                ""nocca"": {7},
                ""nodc"": {8},
                ""nodwell"": {9}
            }}";

            static string Serialize(object obj) => JsonSerializer.Serialize(obj);

            return string.Format(CultureInfo.InvariantCulture, Regex.Replace(template, "\\s+", string.Empty),
                                 Serialize(allowedNetIds.Select(nid => nid.NetworkId)),
                                 Serialize(joinEuiRanges?.Select(r => new[] { r.Min.ToString(), r.Max.ToString() })),
                                 Serialize(region), Serialize(hwspec),
                                 Serialize(new[] { freqRange.Min.AsUInt64, freqRange.Max.AsUInt64 }),
                                 Serialize(dataRates?.Select(dr => new object[] { dr.SpreadingFactor, dr.Bandwidth, dr.DnOnly ? 1 : 0 })),
                                 sx1301Conf ?? defaultSx1301Conf,
                                 Serialize((flags & RouterConfigStationFlags.NoClearChannelAssessment) == RouterConfigStationFlags.NoClearChannelAssessment),
                                 Serialize((flags & RouterConfigStationFlags.NoDutyCycle) == RouterConfigStationFlags.NoDutyCycle),
                                 Serialize((flags & RouterConfigStationFlags.NoDwellTimeLimitations) == RouterConfigStationFlags.NoDwellTimeLimitations));
        }

        [Fact]
        public void RegionConfigurationConverter_Succeeds()
        {
            var region = LnsStationConfiguration.GetRegion(ValidStationConfiguration);
            Assert.Equal(RegionManager.EU868, region);
        }

        [Fact]
        public void RegionConfigurationConverter_Throws_OnEmptyRegion()
        {
            var config = JsonUtil.Strictify(@"{'region':''}");
            Assert.Throws<JsonException>(() => _ = LnsStationConfiguration.GetRegion(config));
        }

        [Fact]
        public void RegionConfigurationConverter_Throws_OnNotSetRegion()
        {
            var config = JsonUtil.Strictify(@"{'region':'NotSet'}");
            Assert.Throws<NotSupportedException>(() => _ = LnsStationConfiguration.GetRegion(config));
        }

        [Theory]
        [InlineData(923000000, 922000000, 200000, 400000, 0)]
        [InlineData(923000000, 922000000, -1600000, -1400000, -1800000)]
        [InlineData(922000000, 920000000, -4700000, -4500000, -5900000)]
        public void RegionConfigurationConverter_CorrectlyReadsAS923Region_WithOffset(
            long radio0Freq, long radio1Freq, long multiSf0If, long multiSf1If, long exptectedOffset)
        {
            var input = @"{{
            'msgtype': 'router_config',
            'NetID': [1],
            'JoinEui': [[0, 18446744073709551615]],
	        'region': 'AS923',
	        'hwspec': 'sx1301/1',
	        'freq_range': [ 920000000, 925000000 ],
            'DRs': [ [ 11, 125, 0 ],
                     [ 10, 125, 0 ],
                     [ 9, 125, 0 ],
                     [ 8, 125, 0 ],
                     [ 7, 125, 0 ],
                     [ 7, 250, 0 ] ],
            'sx1301_conf': [
                {{
                    'radio_0': {{
                        'enable': true,
                        'type': 'SX1257',
                        'freq': {0},
                        'rssi_offset': -166.0,
                        'tx_enable': true,
                        'tx_freq_min': 920000000,
                        'th_freq_max': 923400000
                    }},
                    'radio_1': {{
                        'enable': true,
                        'type': 'SX1257',
                        'freq': {1},
                        'rssi_offset': -166.0,
                        'tx_enable': false,
                    }},
                    'chan_FSK': {{
                        'enable': true,
                        'radio': 1,
                        'if': -1800000
                    }},
                    'chan_Lora_std': {{
                        'enable': true,
                        'radio': 1,
                        'if': -1800000,
                        'bandwidth': 250000,
                        'spread_factor': 7
                    }},
                    'chan_multiSF_0': {{
                        'enable': true,
                        'radio': 0,
                        'if': {2}
                    }},
                    'chan_multiSF_1': {{
                        'enable': true,
                        'radio': 0,
                        'if': {3}
                    }},
                    'chan_multiSF_2': {{
                        'enable': true,
                        'radio': 1,
                        'if': 0
                    }},
                    'chan_multiSF_3': {{
                        'enable': true,
                        'radio': 0,
                        'if': -1800000
                    }},
                    'chan_multiSF_4': {{
                        'enable': true,
                        'radio': 0,
                        'if': -1800000
                    }},
                    'chan_multiSF_5': {{
                        'enable': true,
                        'radio': 0,
                        'if': 0
                    }},
                    'chan_multiSF_6': {{
                        'enable': true,
                        'radio': 0,
                        'if': -1800000
                    }},
                    'chan_multiSF_7': {{
                        'enable': true,
                        'radio': 0,
                        'if': -1800000
                    }}
                }}
            ],
            'nocca': true,
            'nodc': true,
            'nodwell': true}}";

            var config = JsonUtil.Strictify(string.Format(CultureInfo.InvariantCulture, input, radio0Freq, radio1Freq, multiSf0If, multiSf1If));
            var region = LnsStationConfiguration.GetRegion(config);

            Assert.Equal(typeof(RegionAS923), region.GetType());
            Assert.Equal(exptectedOffset, ((RegionAS923)region).FrequencyOffset);
        }
    }
}
