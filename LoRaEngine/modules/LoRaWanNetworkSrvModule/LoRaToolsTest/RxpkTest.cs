// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
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

        [Theory]
        [InlineData(868.1, "SF12BW125", "SF12BW125")]
        [InlineData(868.1, "SF8BW125", "SF8BW125")]
        [InlineData(868.1, "SF7BW250", "SF7BW250")]
        [InlineData(800, "SF12BW125", null)]
        [InlineData(868.1, "SF36BW125", null)]
        public void CheckEUValidUpstreamRxpk(double frequency, string datr, string expectedDatr)
        {
            string jsonUplink =
                "{\"rxpk\":[{\"time\":\"2013-03-31T16:21:17.528002Z\"," +
                "\"tmst\":3512348611," +
                "\"chan\":2," +
                "\"rfch\":0," +
                $"\"freq\": {frequency}," +
                "\"stat\":1," +
                "\"modu\":\"LORA\"," +
                $"\"datr\":\"{datr}\"," +
                "\"codr\":\"4/6\"," +
                "\"rssi\":-35," +
                "\"lsnr\":5.1," +
                "\"size\":32," +
                "\"data\":\"AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=\"," +
               "\"custom_prop_a\":\"a\"," +
               "\"custom_prop_b\":10" +
               " }]}";
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var request = Encoding.Default.GetBytes(jsonUplink);
            var rxpks = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(request).ToArray());
            var downstream = RegionManager.EU868.GetDownstreamDR(rxpks[0]);
            Assert.Equal(expectedDatr, downstream);
        }

        [Theory]
        [InlineData(915, "SF10BW125", "SF10BW500")]
        [InlineData(915, "SF7BW125", "SF7BW500")]
        [InlineData(915, "SF8BW125", "SF8BW500")]
        [InlineData(900, "SF12BW125", null)]
        [InlineData(915, "SF36BW125", null)]
        public void CheckUSValidUpstreamRxpk(double frequency, string datr, string expectedDatr)
        {
            string jsonUplink =
                "{\"rxpk\":[{\"time\":\"2013-03-31T16:21:17.528002Z\"," +
                "\"tmst\":3512348611," +
                "\"chan\":2," +
                "\"rfch\":0," +
                $"\"freq\": {frequency}," +
                "\"stat\":1," +
                "\"modu\":\"LORA\"," +
                $"\"datr\":\"{datr}\"," +
                "\"codr\":\"4/6\"," +
                "\"rssi\":-35," +
                "\"lsnr\":5.1," +
                "\"size\":32," +
                "\"data\":\"AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=\"," +
               "\"custom_prop_a\":\"a\"," +
               "\"custom_prop_b\":10" +
               " }]}";
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            var request = Encoding.Default.GetBytes(jsonUplink);
            var rxpks = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(request).ToArray());
            var downstream = RegionManager.US915.GetDownstreamDR(rxpks[0]);
            Assert.Equal(expectedDatr, downstream);
        }

        [Theory]
        [InlineData(LoRaRegionType.EU868, 0)]
        [InlineData(LoRaRegionType.EU868, 1)]
        [InlineData(LoRaRegionType.EU868, 3)]
        [InlineData(LoRaRegionType.EU868, 5, false)]
        [InlineData(LoRaRegionType.US915, 0)]
        [InlineData(LoRaRegionType.US915, 4)]
        [InlineData(LoRaRegionType.US915, 2)]
        [InlineData(LoRaRegionType.US915, 13, false)]
        [InlineData(LoRaRegionType.US915, 10, false)]
        public void Check_Correct_RXPK_Datr_Are_Accepted(LoRaRegionType loRaRegionType, uint datrIndex, bool upstream = true)
        {
            if (loRaRegionType == LoRaRegionType.EU868)
            {
                if (upstream)
                {
                    Assert.True(RegionManager.EU868.RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue(datrIndex));
                }
                else
                {
                    Assert.True(RegionManager.EU868.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(datrIndex));
                }
            }
            else
            {
                if (upstream)
                {
                    Assert.True(RegionManager.US915.RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue(datrIndex));
                }
                else
                {
                    Assert.True(RegionManager.US915.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(datrIndex));
                }
            }
        }

        [Theory]
        [InlineData(LoRaRegionType.EU868, 8)]
        [InlineData(LoRaRegionType.EU868, 10)]
        [InlineData(LoRaRegionType.EU868, 8, false)]
        [InlineData(LoRaRegionType.EU868, 10, false)]
        [InlineData(LoRaRegionType.US915, 5)]
        [InlineData(LoRaRegionType.US915, 7)]
        [InlineData(LoRaRegionType.US915, 14)]
        [InlineData(LoRaRegionType.US915, 10)]
        [InlineData(LoRaRegionType.US915, 2, false)]
        [InlineData(LoRaRegionType.US915, 12)]
        public void Check_incorrect_RXPK_Datr_Are_Refused(LoRaRegionType loRaRegionType, uint datrIndex, bool upstream = true)
        {
            if (loRaRegionType == LoRaRegionType.EU868)
            {
                if (upstream)
                {
                    Assert.False(RegionManager.EU868.RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue(datrIndex));
                }
                else
                {
                    Assert.False(RegionManager.EU868.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(datrIndex));
                }
            }
            else
            {
                if (upstream)
                {
                    Assert.False(RegionManager.US915.RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue(datrIndex));
                }
                else
                {
                    Assert.False(RegionManager.US915.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(datrIndex));
                }
            }
        }
    }
}
