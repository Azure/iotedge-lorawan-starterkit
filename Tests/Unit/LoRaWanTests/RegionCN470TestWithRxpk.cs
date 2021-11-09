// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System;
    using global::LoRaTools.Regions;
    using Xunit;

    [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
    public class RegionCN470TestWithRxpk : RegionTestBaseRxpk
    {
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

        [Theory]
        [InlineData("SF11BW125", 31)]
        [InlineData("SF10BW125", 94)]
        [InlineData("SF9BW125", 192)]
        [InlineData("SF8BW125", 250)]
        [InlineData("SF7BW125", 250)]
        [InlineData("SF7BW500", 250)]
        [InlineData("50", 250)]
        public void TestMaxPayloadLength(string datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData(null, null, 0, null, "SF11BW125")]
        [InlineData(null, null, 8, null, "SF11BW125")]
        [InlineData(null, null, 10, null, "SF11BW125")]
        [InlineData(null, null, 19, null, "SF11BW125")]
        [InlineData(null, null, null, 5, "SF11BW125")]
        [InlineData(null, null, null, 12, "SF11BW125")]
        [InlineData(null, null, 10, 14, "SF11BW125")]
        [InlineData(null, (ushort)2, 0, null, "SF10BW125")]
        [InlineData("SF9BW125", null, 0, null, "SF9BW125")]
        [InlineData("SF9BW125", (ushort)2, 0, null, "SF10BW125")]
        [InlineData("SF8BW125", (ushort)3, 0, 8, "SF9BW125")]
        public void TestRX2DataRate(string nwksrvrx2dr, ushort? rx2drfromtwins, int? reportedJoinChannel, int? desiredJoinChannel, string expectedDr)
        {
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            TestDownstreamRX2DataRate(nwksrvrx2dr, rx2drfromtwins, expectedDr, deviceJoinInfo);
        }

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegionRxpk("SF11BW125", 470.3);
        }
    }
}
