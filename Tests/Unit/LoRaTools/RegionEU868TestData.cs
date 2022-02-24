// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Linq;
    using global::LoRaTools.Regions;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public static class RegionEU868TestData
    {
        private static readonly Region Region = RegionManager.EU868;
        private static readonly DataRateIndex[] DataRates = { DR0, DR1, DR2, DR3, DR4, DR5, DR6 };
        private static readonly Hertz[] Frequencies = { Mega(868.1), Mega(868.3), Mega(868.5) };

        public static TheoryData<Region, Hertz, DataRateIndex, Hertz> TestRegionFrequencyData() =>
            TheoryDataFactory.From(from dr in DataRates
                                   from freq in Frequencies
                                   select (Region, freq, dr, freq));

        public static TheoryData<Region, DataRateIndex, DataRateIndex> TestRegionDataRateData() =>
            TheoryDataFactory.From(from dr in DataRates
                                   select (Region, dr, dr));

        public static TheoryData<Region, DataRateIndex, int> TestRegionDataRateData_InvalidOffset =>
            TheoryDataFactory.From(new[]
            {
                (Region, DR0, 6),
                (Region, DR1, 10)
            });

        public static TheoryData<Region, Hertz, DataRateIndex> TestRegionLimitData =>
            TheoryDataFactory.From(new (Region, Hertz, DataRateIndex)[]
            {
                (Region, Mega( 800  ), DR8),
                (Region, Mega(1023  ), DR10),
                (Region, Mega( 862.1), (DataRateIndex)90),
                (Region, Mega( 860.3), (DataRateIndex)100),
                (Region, Mega( 880  ), (DataRateIndex)100),
            });

        public static TheoryData<Region, DataRateIndex, uint> TestRegionMaxPayloadLengthData =>
            TheoryDataFactory.From(new (Region, DataRateIndex, uint)[]
            {
                (Region, DR0, 59),
                (Region, DR1, 59),
                (Region, DR2, 59),
                (Region, DR3, 123),
                (Region, DR4, 230),
                (Region, DR5, 230),
                (Region, DR6, 230),
                (Region, DR7, 230),
            });

        public static TheoryData<Region, Hertz?, Hertz> TestDownstreamRX2FrequencyData =>
             TheoryDataFactory.From(new (Region, Hertz?, Hertz)[]
             {
                 (Region, null         , Mega(869.525)),
                 (Region, Mega(868.250), Mega(868.250)),
             });

        public static TheoryData<Region, DataRateIndex?, DataRateIndex?, DataRateIndex> TestDownstreamRX2DataRateData =>
            TheoryDataFactory.From<Region, DataRateIndex?, DataRateIndex?, DataRateIndex>(new (Region, DataRateIndex?, DataRateIndex?, DataRateIndex)[]
            {
                (Region, null, null, DR0), // Standard EU.
                (Region, DR3, null, DR3), // nwksrvDR is correctly applied if no device twins.
                (Region, DR3, DR6, DR6), // device twins are applied in priority.
            });

        public static TheoryData<Region, LoRaRegionType> TestTranslateToRegionData =>
           TheoryDataFactory.From(new[]
           {
               (Region, LoRaRegionType.EU868),
               (Region, LoRaRegionType.EU863),
           });

        public static TheoryData<Region, Hertz, int> TestTryGetJoinChannelIndexData =>
            TheoryDataFactory.From(from freq in new Hertz[] { Mega(863), Mega(870) }
                                   select (Region, freq, /* expected index */ -1));

        public static TheoryData<Region, int, bool> TestIsValidRX1DROffsetData =>
           TheoryDataFactory.From(new[]
           {
                (Region, 0, true),
                (Region, 5, true),
                (Region, 6, false),
           });

        public static TheoryData<Region, DataRateIndex, bool, bool> TestIsDRIndexWithinAcceptableValuesData =>
            TheoryDataFactory.From<Region, DataRateIndex, bool, bool>(new[]
            {
                (Region, DR0, true, true),
                (Region, DR1, true, true),
                (Region, DR3, true, true),
                (Region, DR5, false, true),
                (Region, DR8, true, false),
                (Region, DR8, false, false),
                (Region, DR10, true, false),
                (Region, DR10, false, false),
            });
    }
}
