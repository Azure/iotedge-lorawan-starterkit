// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using Xunit;

    public class RegionUS915Test : RegionTestBase
    {
        public RegionUS915Test()
        {
            Region = RegionManager.US915;
        }

        [Theory]
        [PairwiseData]
        public void TestFrequencyAndDataRateDR1To3(
            [CombinatorialValues(0, 1, 2, 3)] ushort inputDr,
            [CombinatorialValues(902.3, 902.5, 902.7, 902.9, 903.1, 903.3, 903.5, 903.7, 903.9)] double inputFreq)
        {
            var inputDrToExpectedDr = new Dictionary<ushort, ushort>
            {
                { 0, 10 },
                { 1, 11 },
                { 2, 12 },
                { 3, 13 }
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
            [CombinatorialValues(4)] ushort inputDr,
            [CombinatorialValues(903, 904.6, 906.2, 907.8, 909.4, 911, 912.6, 914.2)] double inputFreq)
        {
            ushort expectedDr = 13;

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
        [InlineData(700, 0)]
        [InlineData(1024, 2)]
        [InlineData(915, 90)]
        [InlineData(920, 100)]
        public void TestLimit(double freq, ushort datarate)
        {
            TestRegionLimit(freq, datarate);
        }

        [Theory]
        [InlineData(0,  19)]
        [InlineData(1, 61)]
        [InlineData(2, 133)]
        [InlineData(3, 250)]
        [InlineData(4, 250)]
        [InlineData(8,  61)]
        [InlineData(9,  137)]
        [InlineData(10,  250)]
        [InlineData(11, 250)]
        [InlineData(13, 250)]
        public void TestMaxPayloadLength(ushort datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData(null, null, null, 923.3, (ushort)8)] // Standard US.
        [InlineData((ushort)11, null, null, 923.3, (ushort)11)] // Standard EU.
        [InlineData((ushort)11, 920.0, (ushort)12, 920.0, (ushort)12)] // Standard EU.
        public void TestDownstreamRX2(ushort? nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, ushort expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }

        [Fact]
        public void TestTranslateRegionType()
        {
            TestTranslateToRegion(LoRaRegionType.US915);
        }
    }
}
