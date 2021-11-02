// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using LoRaTools.Regions;
    using Xunit;

    [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
    public class RegionEU868TestWithRxpk : RegionTestBaseRxpk
    {
        public RegionEU868TestWithRxpk()
        {
            Region = RegionManager.EU868;
        }

        [Theory]
        [CombinatorialData]
        public void TestFrequencyAndDataRate(
            [CombinatorialValues("SF12BW125", "SF11BW125", "SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125", "SF7BW250")] string inputDr,
            [CombinatorialValues(868.1, 868.3, 868.5)] double inputFreq)
        {
            var expectedDr = inputDr;
            var expectedFreq = inputFreq;

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [InlineData(800, "SF12BW125")]
        [InlineData(1023, "SF8BW125")]
        [InlineData(868.1, "SF0BW125")]
        [InlineData(869.3, "SF32BW543")]
        [InlineData(800, "SF0BW125")]
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
        [InlineData("SF7BW250", 230)]
        [InlineData("50", 230)]
        public void TestMaxPayloadLength(string datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData("", null, null, 869.525, "SF12BW125")] // Standard EU.
        [InlineData("SF9BW125", null, null, 869.525, "SF9BW125")] // nwksrvDR is correctly applied if no device twins.
        [InlineData("SF9BW125", 868.250, (ushort)6, 868.250, "SF7BW250")] // device twins are applied in priority.
        public void TestDownstreamRX2(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegionRxpk("SF9BW125", 868.1);
        }

        [Theory]
        [InlineData(863)]
        [InlineData(870)]
        public void TestTryGetJoinChannelIndexReturns_InvalidIndex(double freq)
        {
            TestTryGetJoinChannelIndexRxpk("SF9BW125", freq, -1);
        }
    }
}
