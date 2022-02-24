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

    public static class RegionCN470RP1TestData
    {
        private static readonly Region Region = RegionManager.CN470RP1;

        public static TheoryData<Region, Hertz, DataRateIndex, Hertz> TestRegionFrequencyData =>
            TheoryDataFactory.From<Region, Hertz, DataRateIndex, Hertz>(new (Region, Hertz, DataRateIndex, Hertz)[]
            {
                (Region, Mega(470.3), DR0, Mega(500.3)),
                (Region, Mega(471.5), DR0, Mega(501.5)),
                (Region, Mega(473.3), DR0, Mega(503.3)),
                (Region, Mega(475.9), DR0, Mega(505.9)),
                (Region, Mega(477.7), DR0, Mega(507.7)),
                (Region, Mega(478.1), DR0, Mega(508.1)),
                (Region, Mega(479.7), DR0, Mega(509.7)),
                (Region, Mega(479.9), DR0, Mega(500.3)),
                (Region, Mega(480.1), DR0, Mega(500.5)),
                (Region, Mega(484.1), DR0, Mega(504.5)),
                (Region, Mega(489.3), DR0, Mega(509.7)),
            });

        public static TheoryData<Region, DataRateIndex, DataRateIndex, int> TestRegionDataRateData =>
           TheoryDataFactory.From<Region, DataRateIndex, DataRateIndex, int>(new[]
           {
               (Region, DR0, DR0, 0),
               (Region, DR1, DR1, 0),
               (Region, DR2, DR2, 0),
               (Region, DR5, DR5, 0),
               (Region, DR1, DR0, 5),
               (Region, DR2, DR1, 1),
               (Region, DR3, DR1, 2),
               (Region, DR3, DR0, 3),
               (Region, DR4, DR2, 2),
               (Region, DR5, DR2, 3),
           });

        public static TheoryData<Region, DataRateIndex, int> TestRegionDataRateData_InvalidOffset =>
           TheoryDataFactory.From(new[]
           {
               (Region, DR0, 6),
               (Region, DR2, 10),
           });

        public static TheoryData<Region, Hertz, DataRateIndex> TestRegionLimitData =>
            TheoryDataFactory.From(new (Region, Hertz, DataRateIndex)[]
            {
                (Region, Mega(467.0), DR6),
                (Region, Mega(469.9), DR7),
                (Region, Mega(510.8), (DataRateIndex)20),
                (Region, Mega(512.3), (DataRateIndex)110),
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
           });

        public static TheoryData<Region, Hertz?, Hertz> TestDownstreamRX2FrequencyData =>
           TheoryDataFactory.From(new (Region, Hertz?, Hertz)[]
           {
               (Region, null       , Mega(505.3)),
               (Region, Mega(505.3), Mega(505.3)),
               (Region, Mega(500.3), Mega(500.3)),
               (Region, Mega(509.7), Mega(509.7)),
           });

        public static TheoryData<Region, DataRateIndex?, DataRateIndex?, DataRateIndex> TestDownstreamRX2DataRateData =>
            TheoryDataFactory.From<Region, DataRateIndex?, DataRateIndex?, DataRateIndex>(new (Region, DataRateIndex?, DataRateIndex?, DataRateIndex)[]
            {
                (Region, null, null, DR0),
                (Region, null, DR2, DR2),
                (Region, null, DR5, DR5),
                (Region, null, DR6, DR0),
                (Region, DR4, null, DR4),
                (Region, DR4, DR5, DR5),
            });

        public static TheoryData<Region, LoRaRegionType> TestTranslateToRegionData =>
           TheoryDataFactory.From(new[] { (Region, LoRaRegionType.CN470RP1) });

        public static TheoryData<Region, Hertz, int> TestTryGetJoinChannelIndexData =>
            TheoryDataFactory.From(from freq in new Hertz[] { Mega(470.3), Mega(489.3), Mega(509.7) }
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
                (Region, DR2, true, true),
                (Region, DR5, true, true),
                (Region, DR6, true, false),
                (Region, DR0, false, true),
                (Region, DR2, false, true),
                (Region, DR5, false, true),
                (Region, DR6, false, false),
                (Region, DR10, false, false),
            });
    }
}
