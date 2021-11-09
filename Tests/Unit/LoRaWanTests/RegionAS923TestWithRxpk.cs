// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using LoRaTools.Regions;
    using Xunit;

    [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
    public class RegionAS923TestWithRxpk : RegionTestBaseRxpk
    {
        public RegionAS923TestWithRxpk()
        {
            Region = new RegionAS923().WithFrequencyOffset(new Hertz(923_200_000), new Hertz(923_400_000));
        }

        [Theory]
        [CombinatorialData]
        public void TestFrequencyAndDataRate(
           [CombinatorialValues("SF12BW125", "SF11BW125", "SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125", "SF7BW250")] string inputDr,
           [CombinatorialValues(923.2, 923.4, 921.4)] double inputFreq)
        {
            var expectedDr = inputDr;
            var expectedFreq = inputFreq;

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [InlineData("SF12BW125", 8)]
        [InlineData("SF11BW125", 9)]
        [InlineData("SF9BW125", 10)]
        public void TestDataRateInvalidOffset(string dataRate, int rx1DrOffset)
        {
            var rxpk = GenerateRxpk(dataRate, 923.2);
            TestRegionDataRateRxpk(rxpk, null, rx1DrOffset);
        }

        [Theory]
        [InlineData(900, "SF12BW125")]
        [InlineData(914.5, "SF8BW125")]
        [InlineData(930, "SF8BW125")]
        [InlineData(923.4, "SF32BW543")]
        [InlineData(925.5, "SF0BW125")]
        public void TestLimit(double freq, string datarate)
        {
            TestRegionLimitRxpk(freq, datarate);
        }

        [Theory]
        [InlineData("SF12BW125", 59)]
        [InlineData("SF11BW125", 59)]
        [InlineData("SF10BW125", 123)]
        [InlineData("SF9BW125", 123)]
        [InlineData("SF8BW125", 230)]
        [InlineData("SF7BW125", 230)]
        [InlineData("SF7BW250", 230)]
        [InlineData("50", 230)]
        public void TestMaxPayloadLength(string datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData("", null, null, 923.2, "SF10BW125")]
        [InlineData("SF9BW125", null, null, 923.2, "SF9BW125")]
        [InlineData("SF9BW125", 925.5, (ushort)1, 925.5, "SF11BW125")]
        public void TestDownstreamRX2(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegionRxpk("SF11BW125", 923.2);
        }

        [Theory]
        [InlineData(923)]
        [InlineData(923.4)]
        public void TestTryGetJoinChannelIndex_ReturnsInvalidIndex(double freq)
        {
            TestTryGetJoinChannelIndexRxpk("SF11BW125", freq, -1);
        }
    }
}
