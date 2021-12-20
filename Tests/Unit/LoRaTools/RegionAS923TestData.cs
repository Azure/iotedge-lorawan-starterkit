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
        private static readonly List<DataRateIndex> dataRates = new() { DR0, DR1, DR2, DR3, DR4, DR5, DR6, DR7 };

        private static readonly List<Hertz> frequencies =
            new List<ulong> { 923_200_000, 923_400_000, 921_400_000, 916_600_000, 917_500_000 }
            .Select(fr => new Hertz(fr)).ToList();

        private static readonly DwellTimeLimitedRegion region;
        private static readonly DwellTimeLimitedRegion regionWithDwellTime;

#pragma warning disable CA1810 // Initialize reference type static fields inline (test code is not performance-sensitive)
        static RegionAS923TestData()
#pragma warning restore CA1810 // Initialize reference type static fields inline
        {
            region = new RegionAS923().WithFrequencyOffset(frequencies[0], frequencies[1]);
            region.UseDwellTimeSetting(new DwellTimeSetting(false, false, 0));
            regionWithDwellTime = new RegionAS923().WithFrequencyOffset(frequencies[0], frequencies[1]);
            regionWithDwellTime.UseDwellTimeSetting(new DwellTimeSetting(true, true, 0));
        }

        public static IEnumerable<object[]> TestRegionFrequencyData()
        {
            foreach (var dr in dataRates)
            {
                foreach (var freq in frequencies)
                    yield return new object[] { region, freq, dr, freq };
            }
        }

        public static IEnumerable<object[]> TestRegionDataRateData() =>
            new List<object[]>
            {
                // No DwellTime limit
                new object[] { region, 0, 0, 0 },
                new object[] { region, 1, 1, 0 },
                new object[] { region, 6, 6, 0 },
                new object[] { region, 2, 1, 1 },
                new object[] { region, 3, 1, 2 },
                new object[] { region, 4, 2, 2 },
                new object[] { region, 5, 7, 7 },
                new object[] { region, 6, 7, 6 },
                new object[] { region, 3, 4, 6 },
                // With DwellTime limit
                // new object[] { regionWithDwellTime, 0, 2, 0 },
                // new object[] { regionWithDwellTime, 1, 2, 0 },
                new object[] { regionWithDwellTime, 6, 6, 0 },
                new object[] { regionWithDwellTime, 2, 2, 1 },
                new object[] { regionWithDwellTime, 3, 2, 2 },
                new object[] { regionWithDwellTime, 4, 2, 2 },
                new object[] { regionWithDwellTime, 5, 7, 7 },
                new object[] { regionWithDwellTime, 6, 7, 6 },
                new object[] { regionWithDwellTime, 3, 4, 6 },
            };

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
           new List<object[]>
           {
               new object[] { region, 1, 8 },
               new object[] { region, 1, 9 },
               new object[] { regionWithDwellTime, 2, 10 },
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
            select new object[] { region, x.Frequency, x.DataRate };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, 0, 59 },
               new object[] { region, 1, 59 },
               new object[] { region, 2, 123 },
               new object[] { region, 3, 123 },
               new object[] { region, 4, 230 },
               new object[] { region, 5, 230 },
               new object[] { region, 6, 230 },
               new object[] { region, 7, 230 },
               new object[] { regionWithDwellTime, 2, 19 },
               new object[] { regionWithDwellTime, 3, 61 },
               new object[] { regionWithDwellTime, 4, 133 },
               new object[] { regionWithDwellTime, 5, 230 },
               new object[] { regionWithDwellTime, 6, 230 },
               new object[] { regionWithDwellTime, 7, 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new (Hertz? NwkSrvRx2Freq, Hertz ExpectedFreq)[]
           {
               (null       , Mega(923.2)),
               (Mega(923.4), Mega(923.4)),
               (Mega(925.0), Mega(925.0)),
           }
           select new object[] { region, x.NwkSrvRx2Freq, x.ExpectedFreq };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { region, null, null, 2 },
                new object[] { region, null, DR2, 2 },
                new object[] { region, null, DR5, 5 },
                new object[] { region, DR3, null, 3 },
                new object[] { region, DR3, DR4, 4 },
                new object[] { region, DR2, DR3, 3 },
                new object[] { region, null, DR9, 2 },
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.AS923 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from freq in new Hertz[] { Mega(923.4), Mega(928.0) }
            select new object[] { region, freq, /* expected index */ -1 };

        public static IEnumerable<object[]> TestIsValidRX1DROffsetData =>
           new List<object[]>
           {
                new object[] { region, 0, true },
                new object[] { region, 7, true },
                new object[] { region, 8, false },
                new object[] { region, 10, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { region, DR0, true, true },
                new object[] { region, DR2, true, true },
                new object[] { region, DR7, true, true },
                new object[] { region, DR0, false, true },
                new object[] { region, DR2, false, true },
                new object[] { region, DR7, false, true },
                new object[] { region, DR9, true, false },
                new object[] { region, DR10, false, false },
                new object[] { region, null, false, false },
                new object[] { regionWithDwellTime, DR0, false, false },
                new object[] { regionWithDwellTime, DR0, true, true },
                new object[] { regionWithDwellTime, DR1, false, false },
                new object[] { regionWithDwellTime, DR1, true, true },
                new object[] { regionWithDwellTime, DR2, false, true },
                new object[] { regionWithDwellTime, DR2, true, true }
            };
    }
}
