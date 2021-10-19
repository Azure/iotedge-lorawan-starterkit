// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test.BasicsStation
{
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
    }
}
