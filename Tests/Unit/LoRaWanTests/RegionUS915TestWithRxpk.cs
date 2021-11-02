// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using Xunit;

    [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
    public class RegionUS915TestWithRxpk : RegionTestBase
    {
        private static readonly Region region = RegionManager.US915;

        public RegionUS915TestWithRxpk()
        {
            Region = RegionManager.US915;
        }

        [Theory]
        [PairwiseData]
        public void TestFrequencyAndDataRateDR1To3(
            [CombinatorialValues("SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125")] string inputDr,
            [CombinatorialValues(902.3, 902.5, 902.7, 902.9, 903.1, 903.3, 903.5, 903.7, 903.9)] double inputFreq)
        {
            var inputDrToExpectedDr = new Dictionary<string, string>
            {
                {"SF10BW125", "SF10BW500" },
                {"SF9BW125", "SF9BW500" },
                {"SF8BW125", "SF8BW500" },
                {"SF7BW125", "SF7BW500" }
            };

            var inputFreqToExpectedFreq = new Dictionary<double, double>
            {
                { 902.3, 923.3 },
                { 902.5, 923.9 },
                { 902.7, 924.5 },
                { 902.9, 925.1 },
                { 903.1, 925.7 },
                { 903.3, 926.3 },
                { 903.5, 926.9 },
                { 903.7, 927.5 },
                { 903.9, 923.3 },
                { 904.1, 923.9 },
                { 904.3, 924.5 },
            };

            var expectedDr = inputDrToExpectedDr[inputDr];
            var expectedFreq = inputFreqToExpectedFreq[inputFreq];

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [PairwiseData]
        public void TestFrequencyAndDataRateDR4(
            [CombinatorialValues("SF8BW500")] string inputDr,
            [CombinatorialValues(903, 904.6, 906.2, 907.8, 909.4, 911, 912.6, 914.2)] double inputFreq)
        {
            var expectedDr = "SF7BW500";

            var inputFreqToExpectedFreq = new Dictionary<double, double>
            {
                { 903,   923.3 },
                { 904.6, 923.9 },
                { 906.2, 924.5 },
                { 907.8, 925.1 },
                { 909.4, 925.7 },
                { 911,   926.3 },
                { 912.6, 926.9 },
                { 914.2, 927.5 },
            };

            var expectedFreq = inputFreqToExpectedFreq[inputFreq];

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [InlineData(700, "SF10BW125")]
        [InlineData(1024, "SF8BW125")]
        [InlineData(915, "SF0BW125")]
        [InlineData(920, "SF30BW400")]
        public void TestLimit(double freq, string datarate)
        {
            TestRegionLimitRxpk(freq, datarate);
        }

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
          new List<object[]>
          {
               new object[] { region, "SF10BW125",19 },
               new object[] { region, "SF9BW125", 61 },
               new object[] { region, "SF8BW125", 133 },
               new object[] { region, "SF7BW125", 250 },
               new object[] { region, "SF8BW500", 250 },
               new object[] { region, "SF12BW500",61 },
               new object[] { region, "SF11BW500",137 },
               new object[] { region, "SF10BW500", 250 },
               new object[] { region, "SF9BW500",  250 },
               new object[] { region, "SF7BW500", 250 },
          };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateRxpkData =>
           new List<object[]>
           {
                new object[] { region, "", null, "SF12BW500" },
                new object[] { region, "SF9BW500", null, "SF9BW500" },
                new object[] { region, "SF9BW500", (ushort)12, "SF8BW500" },
           };

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegionRxpk("SF9BW500", 902.3);
        }

        [Theory]
        [InlineData(902.3)]
        [InlineData(927.5)]
        public void TestTryGetJoinChannelIndex_ReturnsInvalidIndex(double freq)
        {
            TestTryGetJoinChannelIndexRxpk("SF12BW500", freq, -1);
        }
    }
}
