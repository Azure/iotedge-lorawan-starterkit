// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.LoRaPhysical
{
    using System.Linq;
    using System.Text;
    using global::LoRaTools.LoRaPhysical;
    using Xunit;

    public class RxpkTest
    {
        [Fact]
        public void When_Creating_From_Json_Has_Correct_Value()
        {
            var jsonUplink = @"{ ""rxpk"":[
                {
                    ""time"":""2013-03-31T16:21:17.528002Z"",
                    ""tmst"":3512348611,
                    ""chan"":2,
                    ""rfch"":0,
                    ""freq"":866.349812,
                    ""stat"":1,
                    ""modu"":""LORA"",
                    ""datr"":""SF7BW125"",
                    ""codr"":""4/6"",
                    ""rssi"":-35,
                    ""lsnr"":5.1,
                    ""size"":32,
                    ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }]}";

            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var request = Encoding.Default.GetBytes(jsonUplink);
            var rxpks = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(request).ToArray());
            _ = Assert.Single(rxpks);
            Assert.Equal(2U, rxpks[0].Chan);
        }

        [Fact]
        public void When_Creating_From_Json_With_Custom_Elements_Has_Correct_Value()
        {
            var jsonUplink = @"{ ""rxpk"":[
                {
                    ""time"":""2013-03-31T16:21:17.528002Z"",
                    ""tmst"":3512348611,
                    ""chan"":2,
                    ""rfch"":0,
                    ""freq"":866.349812,
                    ""stat"":1,
                    ""modu"":""LORA"",
                    ""datr"":""SF7BW125"",
                    ""codr"":""4/6"",
                    ""rssi"":-35,
                    ""lsnr"":5.1,
                    ""size"":32,
                    ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI="",
                    ""custom_prop_a"":""a"",
                    ""custom_prop_b"":10
                }]}";

            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var request = Encoding.Default.GetBytes(jsonUplink);
            var rxpks = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(request).ToArray());
            _ = Assert.Single(rxpks);
            Assert.Equal(2, rxpks[0].ExtraData.Count);
            Assert.Contains("custom_prop_a", rxpks[0].ExtraData.Keys);
            Assert.Contains("custom_prop_b", rxpks[0].ExtraData.Keys);
            Assert.Equal("a", rxpks[0].ExtraData["custom_prop_a"]);
            Assert.Equal(10L, rxpks[0].ExtraData["custom_prop_b"]);
        }

        [Fact]
        public void Multiple_Rxpk_Are_Detected_Correctly()
        {
            var jsonUplink = @"{""rxpk"":[{""tmst"":373051724,""time"":""2020-02-19T04:08:57.265951Z"",""chan"":0,""rfch"":0,""freq"":923.200000,""stat"":1,""modu"":""LORA"",""datr"":""SF9BW125"",""codr"":""4/5"",""lsnr"":12.5,""rssi"":-47,""size"":21,""data"":""gAMAABKgmAAIAvEgIbhjS0LBeM/d""},{""tmst"":373053772,""time"":""2020-02-19T04:08:57.265951Z"",""chan"":6,""rfch"":0,""freq"":923.000000,""stat"":-1,""modu"":""LORA"",""datr"":""SF9BW125"",""codr"":""4/5"",""lsnr"":-13.0,""rssi"":-97,""size"":21,""data"":""gJni7n4+wQBUl/E0sO4vB4gFePx7""}]}";
            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var request = Encoding.Default.GetBytes(jsonUplink);
            var rxpks = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(request).ToArray());
            Assert.Equal(2, rxpks.Count);
        }
    }
}
