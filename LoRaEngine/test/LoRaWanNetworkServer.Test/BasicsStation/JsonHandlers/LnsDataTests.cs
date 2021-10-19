// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test.BasicsStation.JsonHandlers
{
    using LoRaWan;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using Xunit;

    public class LnsDataTests
    {
        [Theory]
        [InlineData(@"{ ""msgtype"": ""router_config"" }", LnsMessageType.router_config)]
        [InlineData(@"{ ""msgtype"": ""dnmsg"" }", LnsMessageType.dnmsg)]
        [InlineData(@"{ ""msgtype"": ""dntxed"" }", LnsMessageType.dntxed)]
        [InlineData(@"{ ""msgtype"": ""jreq"" }", LnsMessageType.jreq)]
        [InlineData(@"{ ""msgtype"": ""updf"" }", LnsMessageType.updf)]
        [InlineData(@"{ ""msgtype"": ""version"" }", LnsMessageType.version)]
        [InlineData(@"{ ""onePropBefore"": { ""value"": 123 }, ""msgtype"": ""version"" }", LnsMessageType.version)]
        [InlineData(@"{ ""msgtype"": ""version"" }, ""onePropAfter"": { ""value"": 123 }", LnsMessageType.version)]
        internal void ReadMessageType_Succeeds(string json, LnsMessageType expectedMessageType)
        {
            LnsData.ReadMessageType(json, out var messageType);
            Assert.Equal(expectedMessageType, messageType);
        }


        [Theory]
        [InlineData(@"{ ""msgtype"": ""NOTrouter_config"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTdnmsg"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTdntxed"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTjreq"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTupdf"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTversion"" }")]
        [InlineData(@"{ ""onePropBefore"": { ""value"": 123 }, ""msgtype"": ""NOTversion"" }")]
        [InlineData(@"{ ""msgtype"": ""NOTversion"" }, ""onePropAfter"": { ""value"": 123 }")]
        internal void ReadMessageType_Fails(string json)
        {
            Assert.Throws<JsonException>(() => LnsData.ReadMessageType(json, out var messageType));
        }

        [Fact]
        public void WriteRouterConfig()
        {
            const string expected = @"{
                    ""msgtype"": ""router_config"",
                    ""NetID"": [1],
                    ""JoinEui"": [[0, 18446744073709551615]],
	                ""region"": ""EU863"",
	                ""hwspec"": ""sx1301/1"",
	                ""freq_range"": [ 863000000, 870000000 ],
                    ""DRs"": [ [ 11, 125, 0 ],
                               [ 10, 125, 0 ],
                               [ 9, 125, 0 ],
                               [ 8, 125, 0 ],
                               [ 7, 125, 0 ],
                               [ 7, 250, 0 ] ],
                    ""sx1301_conf"": [
                                {
                                    ""radio_0"": {
                                        ""enable"": true,
                                        ""freq"": 867500000
                                    },
                                    ""radio_1"": {
                                        ""enable"": true,
                                        ""freq"": 868500000
                                    },
                                    ""chan_FSK"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": 300000
                                    },
                                    ""chan_Lora_std"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": -200000,
                                        ""bandwidth"": 250000,
                                        ""spread_factor"": 7
                                    },
                                    ""chan_multiSF_0"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": -400000
                                    },
                                    ""chan_multiSF_1"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": -200000
                                    },
                                    ""chan_multiSF_2"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": 0
                                    },
                                    ""chan_multiSF_3"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": -400000
                                    },
                                    ""chan_multiSF_4"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": -200000
                                    },
                                    ""chan_multiSF_5"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": 0
                                    },
                                    ""chan_multiSF_6"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": 200000
                                    },
                                    ""chan_multiSF_7"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": 400000
                                    }
                                }
                            ],
                    ""nocca"": true,
                    ""nodc"": true,
                    ""nodwell"": true}";

            var actual = LnsData.WriteRouterConfig(new NetId[] { new NetId(1) },
                                                   new (JoinEui, JoinEui)[] { (new JoinEui(ulong.MinValue), new JoinEui(ulong.MaxValue)) },
                                                   "EU863",
                                                   "sx1301/1",
                                                   (new Hertz(863000000), new Hertz(870000000)),
                                                   new (DataRate, Hertz, bool)[]
                                                   {
                                                       (new DataRate(11), new Hertz(125), false),
                                                       (new DataRate(10), new Hertz(125), false),
                                                       (new DataRate(9), new Hertz(125), false),
                                                       (new DataRate(8), new Hertz(125), false),
                                                       (new DataRate(7), new Hertz(125), false),
                                                       (new DataRate(7), new Hertz(250), false),
                                                   },
                                                   true,
                                                   true,
                                                   true);

            Assert.Equal(TrimJson(expected), actual);
        }


        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void WriteRouterConfig_WithEmptyOrNullJoinEuiFilter(int? JoinEuiCount)
        {
            const string expected = @"{
                    ""msgtype"": ""router_config"",
                    ""NetID"": [1],
                    ""JoinEui"": [],
	                ""region"": ""EU863"",
	                ""hwspec"": ""sx1301/1"",
	                ""freq_range"": [ 863000000, 870000000 ],
                    ""DRs"": [ [ 11, 125, 0 ],
                               [ 10, 125, 0 ],
                               [ 9, 125, 0 ],
                               [ 8, 125, 0 ],
                               [ 7, 125, 0 ],
                               [ 7, 250, 0 ] ],
                    ""sx1301_conf"": [
                                {
                                    ""radio_0"": {
                                        ""enable"": true,
                                        ""freq"": 867500000
                                    },
                                    ""radio_1"": {
                                        ""enable"": true,
                                        ""freq"": 868500000
                                    },
                                    ""chan_FSK"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": 300000
                                    },
                                    ""chan_Lora_std"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": -200000,
                                        ""bandwidth"": 250000,
                                        ""spread_factor"": 7
                                    },
                                    ""chan_multiSF_0"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": -400000
                                    },
                                    ""chan_multiSF_1"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": -200000
                                    },
                                    ""chan_multiSF_2"": {
                                        ""enable"": true,
                                        ""radio"": 1,
                                        ""if"": 0
                                    },
                                    ""chan_multiSF_3"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": -400000
                                    },
                                    ""chan_multiSF_4"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": -200000
                                    },
                                    ""chan_multiSF_5"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": 0
                                    },
                                    ""chan_multiSF_6"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": 200000
                                    },
                                    ""chan_multiSF_7"": {
                                        ""enable"": true,
                                        ""radio"": 0,
                                        ""if"": 400000
                                    }
                                }
                            ],
                    ""nocca"": true,
                    ""nodc"": true,
                    ""nodwell"": true}";

            var actual = LnsData.WriteRouterConfig(new NetId[] { new NetId(1) },
                                                   JoinEuiCount.HasValue ? Array.Empty<(JoinEui, JoinEui)>() : null,
                                                   "EU863",
                                                   "sx1301/1",
                                                   (new Hertz(863000000), new Hertz(870000000)),
                                                   new (DataRate, Hertz, bool)[]
                                                   {
                                                       (new DataRate(11), new Hertz(125), false),
                                                       (new DataRate(10), new Hertz(125), false),
                                                       (new DataRate(9), new Hertz(125), false),
                                                       (new DataRate(8), new Hertz(125), false),
                                                       (new DataRate(7), new Hertz(125), false),
                                                       (new DataRate(7), new Hertz(250), false),
                                                   },
                                                   true,
                                                   true,
                                                   true);

            Assert.Equal(TrimJson(expected), actual);
        }

        static string TrimJson(string json)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            JsonDocument.Parse(json).WriteTo(writer);
            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        [Theory]
        [InlineData(null, "something", typeof(ArgumentNullException))]
        [InlineData("something", null, typeof(ArgumentNullException))]
        [InlineData("something", "", typeof(ArgumentException))]
        [InlineData("", "something", typeof(ArgumentException))]
        public void WriteRouterConfig_Throws_WhenRegionOrHwspecIsNullOrEmpty(string region, string hwspec, Type type)
        {
            Assert.Throws(type, () => LnsData.WriteRouterConfig(Array.Empty<NetId>(),
                                                                Array.Empty<(JoinEui, JoinEui)>(),
                                                                region,
                                                                hwspec,
                                                                (new Hertz(863000000), new Hertz(870000000)),
                                                                new (DataRate, Hertz, bool)[]
                                                                {
                                                                    (new DataRate(11), new Hertz(125), false)
                                                                }));
        }

        [Fact]
        public void WriteRouterConfig_ThrowsArgumentException_WithInvalidFrequencyRange()
        {
            Assert.Throws<ArgumentException>(() => LnsData.WriteRouterConfig(Array.Empty<NetId>(),
                                                                             Array.Empty<(JoinEui, JoinEui)>(),
                                                                             "region",
                                                                             "hwspec",
                                                                             (new Hertz(0), new Hertz(0)),
                                                                             new (DataRate, Hertz, bool)[]
                                                                             {
                                                                                 (new DataRate(11), new Hertz(125), false)
                                                                             }));
        }

        [Theory]
        [InlineData(null, typeof(ArgumentNullException))]
        [InlineData(0, typeof(ArgumentException))]
        public void WriteRouterConfig_Throws_WithNullOrEmptyDataRates(int? dataRates, Type type)
        {
            Assert.Throws(type, () => LnsData.WriteRouterConfig(Array.Empty<NetId>(),
                                                                Array.Empty<(JoinEui, JoinEui)>(),
                                                                "region",
                                                                "hwspec",
                                                                (new Hertz(863000000), new Hertz(870000000)),
                                                                dataRates.HasValue ? Array.Empty<(DataRate, Hertz, bool)>() : null));
        }
    }
}
