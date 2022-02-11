// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public static class RegionAS923TestData
    {
        private static readonly List<DataRateIndex> DataRates = new() { DR0, DR1, DR2, DR3, DR4, DR5, DR6, DR7 };

        private static readonly List<Hertz> Frequencies =
            new List<ulong> { 923_200_000, 923_400_000, 921_400_000, 916_600_000, 917_500_000 }
            .Select(fr => new Hertz(fr)).ToList();

        private static readonly DwellTimeLimitedRegion Region;
        private static readonly DwellTimeLimitedRegion RegionWithDwellTime;

#pragma warning disable CA1810 // Initialize reference type static fields inline (test code is not performance-sensitive)
        static RegionAS923TestData()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            Region = new RegionAS923().WithFrequencyOffset(Frequencies[0], Frequencies[1]);
            Region.UseDwellTimeSetting(new DwellTimeSetting(false, false, 0));
            RegionWithDwellTime = new RegionAS923().WithFrequencyOffset(Frequencies[0], Frequencies[1]);
            RegionWithDwellTime.UseDwellTimeSetting(new DwellTimeSetting(true, true, 0));
        }

        public static IEnumerable<object[]> TestRegionFrequencyData()
        {
            foreach (var dr in DataRates)
            {
                foreach (var freq in Frequencies)
                    yield return new object[] { Region, freq, dr, freq };
            }
        }

        public static IEnumerable<object[]> TestRegionDataRateData() =>
            new List<object[]>
            {
                // No DwellTime limit
                new object[] { Region, 0, 0, 0 },
                new object[] { Region, 1, 1, 0 },
                new object[] { Region, 6, 6, 0 },
                new object[] { Region, 2, 1, 1 },
                new object[] { Region, 3, 1, 2 },
                new object[] { Region, 4, 2, 2 },
                new object[] { Region, 5, 7, 7 },
                new object[] { Region, 6, 7, 6 },
                new object[] { Region, 3, 4, 6 },
                // With DwellTime limit
                new object[] { RegionWithDwellTime, 0, 2, 0 },
                new object[] { RegionWithDwellTime, 1, 2, 0 },
                new object[] { RegionWithDwellTime, 6, 6, 0 },
                new object[] { RegionWithDwellTime, 2, 2, 1 },
                new object[] { RegionWithDwellTime, 3, 2, 2 },
                new object[] { RegionWithDwellTime, 4, 2, 2 },
                new object[] { RegionWithDwellTime, 5, 7, 7 },
                new object[] { RegionWithDwellTime, 6, 7, 6 },
                new object[] { RegionWithDwellTime, 3, 4, 6 },
            };

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
           new List<object[]>
           {
               new object[] { Region, 1, 8 },
               new object[] { Region, 1, 9 },
               new object[] { RegionWithDwellTime, 1, 10 },
           };

        public static IEnumerable<object[]> TestRegionLimitData =>
            from x in new (Hertz Frequency, ushort DataRate)[]
            {
                (Mega(900.0),   8),
                (Mega(914.5),   9),
                (Mega(930.0),  10),
                (Mega(928.4),  18),
                (Mega(928.5),  90),
                (Mega(928.2), 100),
            }
            select new object[] { Region, x.Frequency, x.DataRate };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { Region, 0, 59 },
               new object[] { Region, 1, 59 },
               new object[] { Region, 2, 123 },
               new object[] { Region, 3, 123 },
               new object[] { Region, 4, 230 },
               new object[] { Region, 5, 230 },
               new object[] { Region, 6, 230 },
               new object[] { Region, 7, 230 },
               new object[] { RegionWithDwellTime, 2, 19 },
               new object[] { RegionWithDwellTime, 3, 61 },
               new object[] { RegionWithDwellTime, 4, 133 },
               new object[] { RegionWithDwellTime, 5, 230 },
               new object[] { RegionWithDwellTime, 6, 230 },
               new object[] { RegionWithDwellTime, 7, 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new (Hertz? NwkSrvRx2Freq, Hertz ExpectedFreq)[]
           {
               (null       , Mega(923.2)),
               (Mega(923.4), Mega(923.4)),
               (Mega(925.0), Mega(925.0)),
           }
           select new object[] { Region, x.NwkSrvRx2Freq, x.ExpectedFreq };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { Region, null, null, 2 },
                new object[] { Region, null, DR2, 2 },
                new object[] { Region, null, DR5, 5 },
                new object[] { Region, DR3, null, 3 },
                new object[] { Region, DR3, DR4, 4 },
                new object[] { Region, DR2, DR3, 3 },
                new object[] { Region, null, DR9, 2 },
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { Region, LoRaRegionType.AS923 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from freq in new Hertz[] { Mega(923.4), Mega(928.0) }
            select new object[] { Region, freq, /* expected index */ -1 };

        public static IEnumerable<object[]> TestIsValidRX1DROffsetData =>
           new List<object[]>
           {
                new object[] { Region, 0, true },
                new object[] { Region, 7, true },
                new object[] { Region, 8, false },
                new object[] { Region, 10, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { Region, DR0, true, true },
                new object[] { Region, DR2, true, true },
                new object[] { Region, DR7, true, true },
                new object[] { Region, DR0, false, true },
                new object[] { Region, DR2, false, true },
                new object[] { Region, DR7, false, true },
                new object[] { Region, DR9, true, false },
                new object[] { Region, DR10, false, false },
                new object[] { RegionWithDwellTime, DR0, false, false },
                new object[] { RegionWithDwellTime, DR0, true, true },
                new object[] { RegionWithDwellTime, DR1, false, false },
                new object[] { RegionWithDwellTime, DR1, true, true },
                new object[] { RegionWithDwellTime, DR2, false, true },
                new object[] { RegionWithDwellTime, DR2, true, true }
            };
    }
}
