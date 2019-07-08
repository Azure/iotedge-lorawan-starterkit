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

    public class RegionImplementationTest
    {
        [Theory]
        [CombinatorialData]
        public void TestPhysicalMappingEU(
      [CombinatorialValues("SF12BW125", "SF11BW125", "SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125", "SF7BW250")]string datr,
      [CombinatorialValues(868.1, 868.3, 868.5)] double freq)
        {
            List<Rxpk> rxpk = GenerateRxpk(datr, freq);

            // in EU in standard parameters the expectation is that the reply arrive at same place.
            var expFreq = freq;
            var expDatr = datr;
            Assert.True(RegionManager.EU868.TryGetUpstreamChannelFrequency(rxpk[0], out double frequency));
            Assert.Equal(frequency, expFreq);
            Assert.Equal(RegionManager.EU868.GetDownstreamDR(rxpk[0]), expDatr);
        }

        [Theory]
        [PairwiseData]
        public void TestPhysicalMappingUSDR1to3(
           [CombinatorialValues("SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125")] string datr,
           [CombinatorialValues(902.3, 902.5, 902.7, 902.9, 903.1, 903.3, 903.5, 903.7, 903.9)] double freq)
        {
            List<Rxpk> rxpk = GenerateRxpk(datr, freq);

            // in EU in standard parameters the expectation is that the reply arrive at same place.
            double expFreq = 0;

            string expDatr = string.Empty;

            switch (datr)
            {
                case "SF10BW125":
                    expDatr = "SF10BW500";
                    break;
                case "SF9BW125":
                    expDatr = "SF9BW500";
                    break;
                case "SF8BW125":
                    expDatr = "SF8BW500";
                    break;
                case "SF7BW125":
                    expDatr = "SF7BW500";
                    break;
                default:
                    break;
            }

            switch (freq)
            {
                case 902.3:
                    expFreq = 923.3;
                    break;
                case 902.5:
                    expFreq = 923.9;
                    break;
                case 902.7:
                    expFreq = 924.5;
                    break;
                case 902.9:
                    expFreq = 925.1;
                    break;
                case 903.1:
                    expFreq = 925.7;
                    break;
                case 903.3:
                    expFreq = 926.3;
                    break;
                case 903.5:
                    expFreq = 926.9;
                    break;
                case 903.7:
                    expFreq = 927.5;
                    break;
                case 903.9:
                    expFreq = 923.3;
                    break;
                case 904.1:
                    expFreq = 923.9;
                    break;
                case 904.3:
                    expFreq = 924.5;
                    break;
                default:
                    break;
            }

            Assert.True(RegionManager.US915.TryGetUpstreamChannelFrequency(rxpk[0], out double frequency));
            Assert.Equal(frequency, expFreq);
            Assert.Equal(RegionManager.US915.GetDownstreamDR(rxpk[0]), expDatr);
        }

        [Theory]
        [PairwiseData]
        public void TestPhysicalMappingUSDR4(
          [CombinatorialValues("SF8BW500")] string datr,
          [CombinatorialValues(903, 904.6, 906.2, 907.8, 909.4, 911, 912.6, 914.2)] double freq)
        {
            List<Rxpk> rxpk = GenerateRxpk(datr, freq);

            // in EU in standard parameters the expectation is that the reply arrive at same place.
            double expFreq = 0;

            string expDatr = "SF7BW500";

            switch (freq)
            {
                case 903:
                    expFreq = 923.3;
                    break;
                case 904.6:
                    expFreq = 923.9;
                    break;
                case 906.2:
                    expFreq = 924.5;
                    break;
                case 907.8:
                    expFreq = 925.1;
                    break;
                case 909.4:
                    expFreq = 925.7;
                    break;
                case 911:
                    expFreq = 926.3;
                    break;
                case 912.6:
                    expFreq = 926.9;
                    break;
                case 914.2:
                    expFreq = 927.5;
                    break;
                default:
                    break;
            }

            Assert.True(RegionManager.US915.TryGetUpstreamChannelFrequency(rxpk[0], out double frequency));
            Assert.Equal(frequency, expFreq);
            Assert.Equal(RegionManager.US915.GetDownstreamDR(rxpk[0]), expDatr);
        }

        private static List<Rxpk> GenerateRxpk(string datr, double freq)
        {
            string jsonUplink =
                @"{ ""rxpk"":[
 	            {
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":" + freq + @",
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""" + datr + @""",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }]}";

            var multiRxpkInput = Encoding.Default.GetBytes(jsonUplink);
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            List<Rxpk> rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(multiRxpkInput).ToArray());
            return rxpk;
        }

        [Theory]
        // freq, dr
        [InlineData(800, "SF12BW125", LoRaRegionType.EU868)]
        [InlineData(1023, "SF8BW125", LoRaRegionType.EU868)]
        [InlineData(868.1, "SF0BW125", LoRaRegionType.EU868)]
        [InlineData(869.3, "SF32BW543", LoRaRegionType.EU868)]
        [InlineData(800, "SF0BW125", LoRaRegionType.EU868)]
        [InlineData(700, "SF10BW125", LoRaRegionType.US915)]
        [InlineData(1024, "SF8BW125", LoRaRegionType.US915)]
        [InlineData(915, "SF0BW125", LoRaRegionType.US915)]
        [InlineData(920, "SF30BW400", LoRaRegionType.US915)]
        public void EnsureRegionLimitTestAreWorking(double freq, string datarate, LoRaRegionType region)
        {
            var rxpk = GenerateRxpk(datarate, freq);
            if (region == LoRaRegionType.EU868)
            {
                Assert.False(RegionManager.EU868.TryGetUpstreamChannelFrequency(rxpk[0], out double frequency) &&
                RegionManager.EU868.GetDownstreamDR(rxpk[0]) != null);
            }

            if (region == LoRaRegionType.US915)
            {
                Assert.False(RegionManager.US915.TryGetUpstreamChannelFrequency(rxpk[0], out double frequency) &&
                RegionManager.US915.GetDownstreamDR(rxpk[0]) != null);
            }
        }

        [Theory]
        [InlineData("SF12BW125", 59)]
        [InlineData("SF11BW125", 59)]
        [InlineData("SF10BW125", 59)]
        [InlineData("SF9BW125", 123)]
        [InlineData("SF8BW125", 230)]
        [InlineData("SF7BW125", 230)]
        [InlineData("SF7BW250", 230)]
        [InlineData("50", 230)]

        public void TestMaxPayloadLengthEU(string datr, uint maxPyldSize)
        {
            Assert.Equal(RegionManager.EU868.GetMaxPayloadSize(datr), maxPyldSize);
        }

        [Theory]
        [InlineData("SF10BW125",  19)]
        [InlineData("SF9BW125",   61)]
        [InlineData("SF8BW125",  133)]
        [InlineData("SF7BW125",  250)]
        [InlineData("SF8BW500",  250)]
        [InlineData("SF12BW500",  61)]
        [InlineData("SF11BW500", 137)]
        [InlineData("SF10BW500", 250)]
        [InlineData("SF9BW500",  250)]
        [InlineData("SF7BW500",  250)]

        public void TestMaxPayloadLengthUS(string datr, uint maxPyldSize)
        {
            Assert.Equal(RegionManager.US915.GetMaxPayloadSize(datr), maxPyldSize);
        }
    }
}
