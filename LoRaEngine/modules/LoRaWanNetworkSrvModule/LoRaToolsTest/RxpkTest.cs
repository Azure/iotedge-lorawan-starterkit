// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools.LoRaPhysical;
    using Xunit;

    public class RxpkTest
    {
        [Fact]
        public void When_Creating_From_Json_Has_Correct_Value()
        {
            string jsonUplink = @"{ ""rxpk"":[
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

            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var request = Encoding.Default.GetBytes(jsonUplink);
            var rxpks = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(request).ToArray());
            Assert.Single(rxpks);
            Assert.Equal(2U, rxpks[0].Chan);
        }

        [Fact]
        public void When_Creating_From_Json_With_Custom_Elements_Has_Correct_Value()
        {
            string jsonUplink = @"{ ""rxpk"":[
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

            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var request = Encoding.Default.GetBytes(jsonUplink);
            var rxpks = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(request).ToArray());
            Assert.Single(rxpks);
            Assert.Equal(2, rxpks[0].ExtraData.Count);
            Assert.Contains("custom_prop_a", rxpks[0].ExtraData.Keys);
            Assert.Contains("custom_prop_b", rxpks[0].ExtraData.Keys);
            Assert.Equal("a", rxpks[0].ExtraData["custom_prop_a"]);
            Assert.Equal(10L, rxpks[0].ExtraData["custom_prop_b"]);
        }
    }
}
