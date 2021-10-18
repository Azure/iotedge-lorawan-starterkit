// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test.BasicsStation
{
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using LoRaWan;
    using LoRaWanTest;
    using Xunit;

    public class LnsJsonTests
    {
        [Fact]
        public void ReadUplinkDataFrame()
        {
            const string json = @"{
                    ""msgtype"": ""updf"",
                    ""MHdr"": 128,
                    ""DevAddr"": 58772467,
                    ""FCtrl"": 0,
                    ""FCnt"": 1,
                    ""FOpts"": """",
                    ""FPort"": 8,
                    ""FRMPayload"": ""DE"",
                    ""MIC"": -1863929443,
                    ""RefTime"": 0.000000,
                    ""DR"": 5,
                    ""Freq"": 868500000,
                    ""upinfo"": {
                        ""rctx"": 0,
                        ""xtime"": 50665495826532251,
                        ""gpstime"": 0,
                        ""fts"": -1,
                        ""rssi"": -67,
                        ""snr"": 6.75,
                        ""rxtime"": 1634552613.092554
                    }
                }";

            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
            _ = reader.Read();
            LnsJson.ReadUplinkDataFrame(reader, out var macHeader, out var devAddr, out var mic);
            Assert.Equal(new MacHeader(128), macHeader);
            Assert.Equal(new DevAddr(0x380CBF3), devAddr);
            Assert.Equal(new Mic(unchecked((uint)-1863929443)), mic);
        }

        [Fact]
        public void WriteRouterConfig()
        {
            // const string expected = @"{""freq_range"":[863000000,870000000],""msgtype"":""router_config"",""JoinEUI"":[[13725282814365013217,13725282814365013219]],""NetID"":[1],""DRs"":[[12,125,0],[11,125,0],[10,125,0],[9,125,0],[8,125,0],[7,125,0],[7,250,0]],""hwspec"":""sx1301/1"",""region"":""EU863"",""nocca"":true,""nodc"":true,""nodwell"":true,""sx1301_conf"":[{""radio_0"":{""enable"":true,""freq"":867500000},""radio_1"":{""enable"":true,""freq"":868500000},""chan_FSK"":{""enable"":true,""radio"":1,""if"":300000},""chan_Lora_std"":{""enable"":true,""radio"":1,""if"":-200000,""bandwidth"":250000,""spread_factor"":7},""chan_multiSF_0"":{""enable"":true,""radio"":1,""if"":-400000},""chan_multiSF_1"":{""enable"":true,""radio"":1,""if"":-200000},""chan_multiSF_2"":{""enable"":true,""radio"":1,""if"":0},""chan_multiSF_3"":{""enable"":true,""radio"":0,""if"":-400000},""chan_multiSF_4"":{""enable"":true,""radio"":0,""if"":-200000},""chan_multiSF_5"":{""enable"":true,""radio"":0,""if"":0},""chan_multiSF_6"":{""enable"":true,""radio"":0,""if"":200000},""chan_multiSF_7"":{""enable"":true,""radio"":0,""if"":400000}}]}";

            const string expected = @"{
                    ""msgtype"": ""router_config"",
                    ""freq_range"": [863000000, 870000000],
                    ""JoinEUI"": [[13725282814365013217, 13725282814365013219]]
                }";

            var actual = LnsJson.WriteRouterConfig(
                freqRange: (new(863000000), new(870000000)),
                joinEuiRanges: new (JoinEui, JoinEui)[]
                {
                    (new(13725282814365013217), new(13725282814365013219))
                });

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
    }
}
