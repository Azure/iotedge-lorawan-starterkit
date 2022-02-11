// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public static class RegionCN470RP1TestData
    {
        private static readonly Region Region = RegionManager.CN470RP1;

        public static IEnumerable<object[]> TestRegionFrequencyData =>
            from f in new (Hertz Input, Hertz Output)[]
            {
                (Mega(470.3), Mega(500.3)),
                (Mega(471.5), Mega(501.5)),
                (Mega(473.3), Mega(503.3)),
                (Mega(475.9), Mega(505.9)),
                (Mega(477.7), Mega(507.7)),
                (Mega(478.1), Mega(508.1)),
                (Mega(479.7), Mega(509.7)),
                (Mega(479.9), Mega(500.3)),
                (Mega(480.1), Mega(500.5)),
                (Mega(484.1), Mega(504.5)),
                (Mega(489.3), Mega(509.7)),
            }
            select new object[] { Region, f.Input, /* data rate */ 0, f.Output };

        public static IEnumerable<object[]> TestRegionDataRateData =>
           new List<object[]>
           {
               new object[] { Region, 0, 0, 0 },
               new object[] { Region, 1, 1, 0 },
               new object[] { Region, 2, 2, 0 },
               new object[] { Region, 5, 5, 0 },
               new object[] { Region, 1, 0, 5 },
               new object[] { Region, 2, 1, 1 },
               new object[] { Region, 3, 1, 2 },
               new object[] { Region, 3, 0, 3 },
               new object[] { Region, 4, 2, 2 },
               new object[] { Region, 5, 2, 3 },
           };

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
           new List<object[]>
           {
               new object[] { Region, 0, 6 },
               new object[] { Region, 2, 10 },
           };

        public static IEnumerable<object[]> TestRegionLimitData =>
            from x in new (Hertz Frequency, ushort DataRate)[]
            {
                (Mega(467.0),   6),
                (Mega(469.9),   7),
                (Mega(510.8),  20),
                (Mega(512.3), 110),
            }
            select new object[] { Region, x.Frequency, x.DataRate };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { Region, 0, 59 },
               new object[] { Region, 1, 59 },
               new object[] { Region, 2, 59 },
               new object[] { Region, 3, 123 },
               new object[] { Region, 4, 230 },
               new object[] { Region, 5, 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new (Hertz? NwkSrvRx2Freq, Hertz ExpectedFreq)[]
           {
               (null       , Mega(505.3)),
               (Mega(505.3), Mega(505.3)),
               (Mega(500.3), Mega(500.3)),
               (Mega(509.7), Mega(509.7)),
           }
           select new object[] { Region, x.NwkSrvRx2Freq, x.ExpectedFreq };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { Region, null, null, DR0 },
                new object[] { Region, null, DR2, DR2 },
                new object[] { Region, null, DR5, DR5 },
                new object[] { Region, null, DR6, DR0 },
                new object[] { Region, DR4, null, DR4 },
                new object[] { Region, DR4, DR5, DR5 },
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { Region, LoRaRegionType.CN470RP1 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from freq in new Hertz[] { Mega(470.3), Mega(489.3), Mega(509.7) }
            select new object[] { Region, freq, /* expected index */ -1 };

        public static IEnumerable<object[]> TestIsValidRX1DROffsetData =>
           new List<object[]>
           {
                new object[] { Region, 0, true },
                new object[] { Region, 5, true },
                new object[] { Region, 6, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { Region, DR0, true, true },
                new object[] { Region, DR2, true, true },
                new object[] { Region, DR5, true, true },
                new object[] { Region, DR6, true, false },
                new object[] { Region, DR0, false, true },
                new object[] { Region, DR2, false, true },
                new object[] { Region, DR5, false, true },
                new object[] { Region, DR6, false, false },
                new object[] { Region, DR10, false, false },
            };
    }
}
