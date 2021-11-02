// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using Xunit;

    [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
    public class RegionEU868TestWithRxpk : RegionTestBase
    {
        private static readonly Region region = RegionManager.EU868;

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

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, "SF12BW125", 59 },
               new object[] { region, "SF11BW125", 59 },
               new object[] { region, "SF10BW125", 59 },
               new object[] { region, "SF9BW125", 123 },
               new object[] { region, "SF8BW125", 230 },
               new object[] { region, "SF7BW125", 230 },
               new object[] { region, "SF7BW250", 230 },
               new object[] { region, "50", 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateRxpkData =>
            new List<object[]>
            {
                new object[] { region, "", null, "SF12BW125" }, // Standard EU.
                new object[] { region, "SF9BW125", null, "SF9BW125" }, // nwksrvDR is correctly applied if no device twins.
                new object[] { region, "SF9BW125", (ushort)6, "SF7BW250" }, // device twins are applied in priority.
            };

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
