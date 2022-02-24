// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using LoRaWan.Tests.Common;
    using Xunit;
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

        public static TheoryData<DwellTimeLimitedRegion, Hertz, DataRateIndex, Hertz> TestRegionFrequencyData() =>
            TheoryDataFactory.From(from dr in DataRates
                                   from freq in Frequencies
                                   select (Region, freq, dr, freq));

        public static TheoryData<DwellTimeLimitedRegion, DataRateIndex, DataRateIndex, int> TestRegionDataRateData() =>
            TheoryDataFactory.From<DwellTimeLimitedRegion, DataRateIndex, DataRateIndex, int>(new[]
            {
                // No DwellTime limit
                (Region, DR0, DR0, 0),
                (Region, DR1, DR1, 0),
                (Region, DR6, DR6, 0),
                (Region, DR2, DR1, 1),
                (Region, DR3, DR1, 2),
                (Region, DR4, DR2, 2),
                (Region, DR5, DR7, 7),
                (Region, DR6, DR7, 6),
                (Region, DR3, DR4, 6),
                // With DwellTime limit
                (RegionWithDwellTime, DR0, DR2, 0),
                (RegionWithDwellTime, DR1, DR2, 0),
                (RegionWithDwellTime, DR6, DR6, 0),
                (RegionWithDwellTime, DR2, DR2, 1),
                (RegionWithDwellTime, DR3, DR2, 2),
                (RegionWithDwellTime, DR4, DR2, 2),
                (RegionWithDwellTime, DR5, DR7, 7),
                (RegionWithDwellTime, DR6, DR7, 6),
                (RegionWithDwellTime, DR3, DR4, 6),
            });

        public static TheoryData<DwellTimeLimitedRegion, DataRateIndex, int> TestRegionDataRateData_InvalidOffset =>
            TheoryDataFactory.From(new[]
            {
                (Region, DR1, 8),
                (Region, DR1, 9),
                (RegionWithDwellTime, DR1, 10),
            });

        public static TheoryData<Region, Hertz, DataRateIndex> TestRegionLimitData =>
            TheoryDataFactory.From(new (Region, Hertz, DataRateIndex)[]
            {
                (Region, Mega(900.0), DR8),
                (Region, Mega(914.5), DR9),
                (Region, Mega(930.0), DR10),
                (Region, Mega(928.4), (DataRateIndex)18),
                (Region, Mega(928.5), (DataRateIndex)90),
                (Region, Mega(928.2), (DataRateIndex)100),
            });

        public static TheoryData<Region, DataRateIndex, uint> TestRegionMaxPayloadLengthData =>
           TheoryDataFactory.From(new (Region, DataRateIndex, uint)[]
           {
               (Region, DR0, 59),
               (Region, DR1, 59),
               (Region, DR2, 123),
               (Region, DR3, 123),
               (Region, DR4, 230),
               (Region, DR5, 230),
               (Region, DR6, 230),
               (Region, DR7, 230),
               (RegionWithDwellTime, DR2, 19),
               (RegionWithDwellTime, DR3, 61),
               (RegionWithDwellTime, DR4, 133),
               (RegionWithDwellTime, DR5, 230),
               (RegionWithDwellTime, DR6, 230),
               (RegionWithDwellTime, DR7, 230),
           });

        public static TheoryData<Region, Hertz?, Hertz> TestDownstreamRX2FrequencyData =>
           TheoryDataFactory.From(new (Region, Hertz?, Hertz)[]
           {
               (Region, null       , Mega(923.2)),
               (Region, Mega(923.4), Mega(923.4)),
               (Region, Mega(925.0), Mega(925.0)),
           });

        public static TheoryData<Region, DataRateIndex?, DataRateIndex?, DataRateIndex> TestDownstreamRX2DataRateData =>
            TheoryDataFactory.From<Region, DataRateIndex?, DataRateIndex?, DataRateIndex>(new (Region, DataRateIndex?, DataRateIndex?, DataRateIndex)[]
            {
                (Region, null, null, DR2),
                (Region, null, DR2, DR2),
                (Region, null, DR5, DR5),
                (Region, DR3, null, DR3),
                (Region, DR3, DR4, DR4),
                (Region, DR2, DR3, DR3),
                (Region, null, DR9, DR2),
            });

        public static TheoryData<DwellTimeLimitedRegion, LoRaRegionType> TestTranslateToRegionData =>
           TheoryDataFactory.From(new[] { (Region, LoRaRegionType.AS923) });

        public static TheoryData<DwellTimeLimitedRegion, Hertz, int> TestTryGetJoinChannelIndexData =>
            TheoryDataFactory.From(from freq in new Hertz[] { Mega(923.4), Mega(928.0) }
                                   select (Region, freq, /* expected index */ -1));

        public static TheoryData<DwellTimeLimitedRegion, int, bool> TestIsValidRX1DROffsetData =>
           TheoryDataFactory.From(new[]
           {
                (Region, 0, true),
                (Region, 7, true),
                (Region, 8, false),
                (Region, 10, false),
           });

        public static TheoryData<DwellTimeLimitedRegion, DataRateIndex, bool, bool> TestIsDRIndexWithinAcceptableValuesData =>
            TheoryDataFactory.From<DwellTimeLimitedRegion, DataRateIndex, bool, bool>(new[]
            {
                (Region, DR0, true, true),
                (Region, DR2, true, true),
                (Region, DR7, true, true),
                (Region, DR0, false, true),
                (Region, DR2, false, true),
                (Region, DR7, false, true),
                (Region, DR9, true, false),
                (Region, DR10, false, false),
                (RegionWithDwellTime, DR0, false, false),
                (RegionWithDwellTime, DR0, true, true),
                (RegionWithDwellTime, DR1, false, false),
                (RegionWithDwellTime, DR1, true, true),
                (RegionWithDwellTime, DR2, false, true),
                (RegionWithDwellTime, DR2, true, true)
            });
    }
}
