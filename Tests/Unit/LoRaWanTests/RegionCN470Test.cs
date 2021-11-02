// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using Xunit;

    public class RegionCN470Test : RegionTestBase
    {
        private static readonly Region region = RegionManager.CN470;

        public RegionCN470Test()
        {
            Region = RegionManager.CN470;
        }

        [Theory]
        // 20 MHz plan A
        [InlineData(470.3, 483.9, 0)]
        [InlineData(471.5, 485.1, 1)]
        [InlineData(476.5, 490.1, 2)]
        [InlineData(503.9, 490.7, 3)]
        [InlineData(503.5, 490.3, 4)]
        [InlineData(504.5, 491.3, 5)]
        [InlineData(509.7, 496.5, 7)]
        // 20 MHz plan B
        [InlineData(476.9, 476.9, 8)]
        [InlineData(479.9, 479.9, 8)]
        [InlineData(503.1, 503.1, 9)]
        // 26 MHz plan A
        [InlineData(470.3, 490.1, 10)]
        [InlineData(473.3, 493.1, 11)]
        [InlineData(475.1, 490.1, 12)]
        [InlineData(471.1, 490.9, 14)]
        // 26 MHz plan B
        [InlineData(480.3, 500.1, 15)]
        [InlineData(485.1, 500.1, 16)]
        [InlineData(485.3, 500.3, 17)]
        [InlineData(489.7, 504.7, 18)]
        [InlineData(488.9, 503.9, 19)]
        public void TestFrequency(double inputFreq, double outputFreq, int joinChannel)
        {
            TestRegionFrequency(inputFreq, 0, outputFreq, new DeviceJoinInfo(joinChannel));
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(1, 1, 0)]
        [InlineData(2, 2, 0)]
        [InlineData(6, 6, 0)]
        [InlineData(2, 1, 1)]
        [InlineData(3, 1, 2)]
        [InlineData(4, 2, 2)]
        [InlineData(6, 3, 3)]
        [InlineData(6, 6, 7)]
        [InlineData(1, 1, 10)]
        public void TestDataRate(ushort inputDr, ushort outputDr, int rx1DrOffset)
        {
            var freq = 470.3;
            TestRegionDataRate(freq, inputDr, outputDr, rx1DrOffset);
        }

        public static IEnumerable<object[]> TestRegionLimitData =>
           new List<object[]>
           {
               new object[] { region, 470, 0, 0 },
               new object[] { region, 510, 1, 0 },
               new object[] { region, 509, 100, 0 },
               new object[] { region, 490, 110, 0 },
           };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, 1, 31 },
               new object[] { region, 2, 94 },
               new object[] { region, 3, 192 },
               new object[] { region, 4, 250 },
               new object[] { region, 5, 250 },
               new object[] { region, 6, 250 },
               new object[] { region, 7, 250 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           new List<object[]>
           {
               // OTAA devices
               new object[] { null, 485.3, 0, null },
               new object[] { null, 486.9, 1, 9 },
               new object[] { null, 496.5, 7, null },
               new object[] { null, 498.3, 9, 8 },
               new object[] { null, 492.5, 10, null },
               new object[] { null, 492.5, 12, null },
               new object[] { null, 492.5, 14, 14 },
               new object[] { null, 502.5, 17, null },
               new object[] { null, 502.5, 19, 18 },
               new object[] { 498.3, 498.3, 7, null },
               new object[] { 485.3, 485.3, 15, null },
               new object[] { 492.5, 492.5, 15, 15 },
               // ABP devices
               new object[] { null, 486.9, null, 0 },
               new object[] { null, 486.9, null, 7 },
               new object[] { null, 498.3, null, 8 },
               new object[] { null, 498.3, null, 9 },
               new object[] { null, 492.5, null, 14 },
               new object[] { null, 502.5, null, 15 },
               new object[] { null, 502.5, null, 19 },
               new object[] { 486.9, 486.9, null, 12 },
               new object[] { 502.5, 502.5, null, 17 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { region, null, null, 1, 0, null },
                new object[] { region, null, null, 1, 8, null },
                new object[] { region, null, null, 1, 10, null },
                new object[] { region, null, null, 1, 19, null },
                new object[] { region, null, null, 1, null, 5 },
                new object[] { region, null, null, 1, null, 12 },
                new object[] { region, null, null, 1, 10, 14 },
                new object[] { region, null, (ushort)2, 2, 0, null },
                new object[] { region, (ushort)3, null, 3, 0, null },
                new object[] { region, (ushort)3, (ushort)2, 2, 0, null },
                new object[] { region, (ushort)4, (ushort)3, 3, 0, 8 },
                new object[] { region, null, (ushort)9, 1, 11, null },
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.CN470 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
           new List<object[]>
           {
                new object[] { region, 470.9, 0 },
                new object[] { region, 475.7, 3 },
                new object[] { region, 507.3, 6 },
                new object[] { region, 499.9, 9 },
                new object[] { region, 478.3, 14 },
                new object[] { region, 482.3, 16 },
                new object[] { region, 488.3, 19 },
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
                new object[] { region, (ushort)2, true, true },
                new object[] { region, (ushort)7, true, true },
                new object[] { region, (ushort)0, false, true },
                new object[] { region, (ushort)2, false, true },
                new object[] { region, (ushort)7, false, true },
                new object[] { region, (ushort)9, true, false },
                new object[] { region, (ushort)10, false, false },
                new object[] { region, null, false, false },
            };
    }
}
