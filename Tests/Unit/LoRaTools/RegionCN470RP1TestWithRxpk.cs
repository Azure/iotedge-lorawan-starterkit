// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System;
    using global::LoRaTools.Regions;
    using Xunit;
    using static LoRaWan.DataRateIndex;

    [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
    public class RegionCN470RP1TestWithRxpk : RegionTestBaseRxpk
    {
        public RegionCN470RP1TestWithRxpk()
        {
            Region = RegionManager.CN470RP1;
        }

        [Theory]
        [InlineData(470.3, 500.3)]
        [InlineData(471.5, 501.5)]
        [InlineData(473.3, 503.3)]
        [InlineData(475.9, 505.9)]
        [InlineData(477.7, 507.7)]
        [InlineData(478.1, 508.1)]
        [InlineData(479.7, 509.7)]
        [InlineData(479.9, 500.3)]
        [InlineData(480.1, 500.5)]
        [InlineData(484.1, 504.5)]
        [InlineData(489.3, 509.7)]
        public void TestFrequency(double inputFreq, double outputFreq)
        {
            var rxpk = GenerateRxpk("SF12BW125", inputFreq);
            TestRegionFrequencyRxpk(rxpk, outputFreq);
        }

        [Theory]
        [InlineData("SF12BW125", "SF12BW125", 0)]
        [InlineData("SF11BW125", "SF11BW125", 0)]
        [InlineData("SF10BW125", "SF10BW125", 0)]
        [InlineData("SF7BW125", "SF7BW125", 0)]
        [InlineData("SF11BW125", "SF12BW125", 5)]
        [InlineData("SF10BW125", "SF11BW125", 1)]
        [InlineData("SF9BW125", "SF11BW125", 2)]
        [InlineData("SF9BW125", "SF12BW125", 3)]
        [InlineData("SF8BW125", "SF10BW125", 2)]
        [InlineData("SF7BW125", "SF10BW125", 3)]
        public void TestDataRate(string inputDr, string outputDr, int rx1DrOffset)
        {
            var rxpk = GenerateRxpk(inputDr, 470.3);
            TestRegionDataRateRxpk(rxpk, outputDr, rx1DrOffset);
        }

        [Theory]
        [InlineData(6)]
        [InlineData(10)]
        public void GetDownstreamDataRate_ThrowsWhenOffsetInvalid(int rx1DrOffset)
        {
            TestRegionDataRateRxpk_ThrowsWhenOffsetInvalid("SF12BW125", 470.3, rx1DrOffset);
        }

        [Theory]
        [InlineData(467, "SF6BW125")]
        [InlineData(469.9, "SF8FBW127")]
        [InlineData(510.8, "SF2BW125")]
        [InlineData(512.3, "SF30BW125")]
        public void TestLimit(double freq, string datarate)
        {
            TestRegionLimitRxpk(freq, datarate);
        }

        [Theory]
        [InlineData("SF12BW125", 59)]
        [InlineData("SF11BW125", 59)]
        [InlineData("SF10BW125", 59)]
        [InlineData("SF9BW125", 123)]
        [InlineData("SF8BW125", 230)]
        [InlineData("SF7BW125", 230)]
        public void TestMaxPayloadLength(string datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData(null, null, "SF12BW125")]
        [InlineData(null, DR2, "SF10BW125")]
        [InlineData(null, DR5, "SF7BW125")]
        [InlineData(null, DR6, "SF12BW125")]
        [InlineData("SF8BW125", null,"SF8BW125")]
        [InlineData("SF8BW125", DR5, "SF7BW125")]
        [InlineData("SF10BW125", DR1, "SF11BW125")]
        public void TestRX2DataRate(string nwksrvrx2dr, DataRateIndex? rx2drfromtwins, string expectedDr)
        {
            TestDownstreamRX2DataRate(nwksrvrx2dr, rx2drfromtwins, expectedDr);
        }

        [Theory]
        [InlineData(470.3)]
        [InlineData(489.3)]
        [InlineData(509.7)]
        public void TestTryGetJoinChannelIndex_ReturnsInvalidIndex(double freq)
        {
            TestTryGetJoinChannelIndexRxpk("SF12BW125", freq, -1);
        }

        [Theory]
        [InlineData("SF12BW125", true, true)]
        [InlineData("SF10BW125", true, true)]
        [InlineData("SF7BW125", true, true)]
        [InlineData("SF6BW125", true, false)]
        [InlineData("SF12BW125", false, true)]
        [InlineData("SF10BW125", false, true)]
        [InlineData("SF7BW125", false, true)]
        [InlineData("SF6BW125", false, false)]
        [InlineData("SF7BW250", false, false)]
        [InlineData(null, false, false)]
        public void TestIsDRWithinAcceptableValues(string dataRate, bool upstream, bool isValid)
        {
            TestIsDRValueWithinAcceptableValues(dataRate, upstream, isValid);
        }
    }
}
