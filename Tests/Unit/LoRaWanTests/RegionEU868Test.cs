// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using LoRaTools.Regions;
    using System.Collections.Generic;
    using Xunit;

    public class RegionEU868Test : RegionTestBase
    {
        private static readonly Region region = RegionManager.EU868;

        public RegionEU868Test()
        {
            Region = RegionManager.EU868;
        }

        [Theory]
        [CombinatorialData]
        public void TestFrequencyAndDataRate(
            [CombinatorialValues(0, 1, 2, 3, 4, 5, 6)] ushort inputDr,
            [CombinatorialValues(868.1, 868.3, 868.5)] double inputFreq)
        {
            var expectedDr = inputDr;
            var expectedFreq = inputFreq;

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [InlineData(800, 0)]
        [InlineData(1023, 4)]
        [InlineData(868.1, 90)]
        [InlineData(869.3, 100)]
        [InlineData(800, 110)]
        public void TestLimit(double freq, ushort datarate)
        {
            TestRegionLimit(freq, datarate);
        }

        [Theory]
        [InlineData(0, 59)]
        [InlineData(1, 59)]
        [InlineData(2, 59)]
        [InlineData(3, 123)]
        [InlineData(4, 230)]
        [InlineData(5, 230)]
        [InlineData(6, 230)]
        [InlineData(7, 230)]
        public void TestMaxPayloadLength(ushort datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData(null, null, null, 869.525, (ushort)0)] // Standard EU.
        [InlineData((ushort)3, null, null, 869.525, (ushort)3)] // nwksrvDR is correctly applied if no device twins.
        [InlineData((ushort)3, 868.250, (ushort)6, 868.250, (ushort)6)] // device twins are applied in priority.
        public void TestDownstreamRX2(ushort? nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, ushort expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }

        [Fact]
        public void TestTranslateRegionType()
        {
            TestTranslateToRegion(LoRaRegionType.EU868);
        }

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            new List<object[]>
            {
                new object[] { region, 863, -1 },
                new object[] { region, 870, -1 },
            };

        public static IEnumerable<object[]> TestIsValidRX1DROffsetData =>
           new List<object[]>
           {
                new object[] { region, 0, true },
                new object[] { region, 5, true },
                new object[] { region, 6, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { region, (ushort)0, true, true },
                new object[] { region, (ushort)1, true, true },
                new object[] { region, (ushort)3, true, true },
                new object[] { region, (ushort)5, false, true },
                new object[] { region, (ushort)8, true, false },
                new object[] { region, (ushort)8, false, false },
                new object[] { region, (ushort)10, true, false },
                new object[] { region, (ushort)10, false, false },
                new object[] { region, null, false, false },
            };
    }
}
