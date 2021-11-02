// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using LoRaTools.Regions;
    using System;
    using System.Collections.Generic;
    using Xunit;

    [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
    public class RegionCN470TestWithRxpk : RegionTestBase
    {
        private static readonly Region region = RegionManager.CN470;
        public RegionCN470TestWithRxpk()
        {
            Region = RegionManager.CN470;
        }

        [Theory]
        [InlineData(470.9, 0)]
        [InlineData(475.7, 3)]
        [InlineData(507.3, 6)]
        [InlineData(499.9, 9)]
        [InlineData(478.3, 14)]
        [InlineData(482.3, 16)]
        [InlineData(488.3, 19)]
        public void TestTryGetJoinChannelIndex_ReturnsValidIndex(double freq, int expectedIndex)
        {
            var rxpk = GenerateRxpk("SF12BW125", freq);
            Assert.True(Region.TryGetJoinChannelIndex(rxpk[0], out var channelIndex));
            Assert.Equal(expectedIndex, channelIndex);
        }

        [Theory]
        // 20 MHz plan A
        [InlineData(470.3, 483.9, 0)]
        [InlineData(476.5, 490.1, 1)]
        [InlineData(503.5, 490.3, 4)]
        [InlineData(504.5, 491.3, 5)]
        [InlineData(509.7, 496.5, 7)]
        // 20 MHz plan B
        [InlineData(476.9, 476.9, 8)]
        [InlineData(503.1, 503.1, 9)]
        // 26 MHz plan A
        [InlineData(470.3, 490.1, 10)]
        [InlineData(475.1, 490.1, 12)]
        [InlineData(471.1, 490.9, 14)]
        // 26 MHz plan B
        [InlineData(480.3, 500.1, 15)]
        [InlineData(489.7, 504.7, 17)]
        [InlineData(488.9, 503.9, 19)]
        public void TestFrequency(double inputFreq, double outputFreq, int joinChannel)
        {
            var rxpk = GenerateRxpk("SF12BW125", inputFreq);
            TestRegionFrequencyRxpk(rxpk, outputFreq, new DeviceJoinInfo(joinChannel));
        }

        [Theory]
        [InlineData("SF12BW125", "SF12BW125", 0)]
        [InlineData("SF11BW125", "SF11BW125", 0)]
        [InlineData("SF10BW125", "SF10BW125", 0)]
        [InlineData("SF7BW500", "SF7BW500", 0)]
        [InlineData("SF10BW125", "SF11BW125", 1)]
        [InlineData("SF8BW125", "SF10BW125", 2)]
        [InlineData("SF7BW500", "SF9BW125", 3)]
        [InlineData("SF7BW500", "SF7BW500", 10)]
        public void TestDataRate(string inputDr, string outputDr, int rx1DrOffset)
        {
            var rxpk = GenerateRxpk(inputDr, 470.3);
            TestRegionDataRateRxpk(rxpk, outputDr, rx1DrOffset);
        }

        [Theory]
        [InlineData(470, "SF12BW125")]
        [InlineData(510, "SF11BW125")]
        [InlineData(509, "SF2BW125")]
        [InlineData(490, "SF30BW125")]
        public void TestLimit(double freq, string datarate)
        {
            TestRegionLimitRxpk(freq, datarate, new DeviceJoinInfo(0));
        }

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, "SF11BW125", 31 },
               new object[] { region, "SF10BW125", 94 },
               new object[] { region, "SF9BW125",  192 },
               new object[] { region, "SF8BW125",  250 },
               new object[] { region, "SF7BW125",  250 },
               new object[] { region, "SF7BW500", 250 },
               new object[] { region, "50", 250 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateRxpkData =>
           new List<object[]>
           {
               new object[] { region, null, null, "SF11BW125", 0, null },
               new object[] { region, null, null, "SF11BW125", 8, null },
               new object[] { region, null, null, "SF11BW125", 10, null },
               new object[] { region, null, null, "SF11BW125", 19, null },
               new object[] { region, null, null, "SF11BW125", null, 5 },
               new object[] { region, null, null, "SF11BW125", null, 12 },
               new object[] { region, null, null, "SF11BW125", 10, 14 },
               new object[] { region, null, (ushort)2, "SF10BW125", 0, null },
               new object[] { region, "SF9BW125", null, "SF9BW125", 0, null },
               new object[] { region, "SF9BW125", (ushort)2, "SF10BW125", 0, null },
               new object[] { region, "SF8BW125", (ushort)3, "SF9BW125", 0, 8 },
               new object[] { region, null, (ushort)9, "SF11BW125", 11, null },
           };

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegionRxpk("SF11BW125", 470.3);
        }
    }
}
