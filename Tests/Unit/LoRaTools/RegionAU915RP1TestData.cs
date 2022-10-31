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

    public static class RegionAU915RP1TestData
    {
        private static readonly Region Region = RegionManager.AU915RP1;

        public static readonly TheoryData<Region, Hertz, DataRateIndex, Hertz> TestRegionFrequencyDataDR0To5 =
            TheoryDataFactory.From(from dr in new[] { DR0, DR1, DR2, DR3, DR4, DR5 }
                                   from freq in new (double Input, double Output)[]
                                   {
                                       (915.2, 923.3),
                                       (915.4, 923.9),
                                       (915.6, 924.5),
                                       (915.8, 925.1),
                                       (916.0, 925.7),
                                       (916.2, 926.3),
                                       (916.4, 926.9),
                                       (916.6, 927.5),
                                       (916.8, 923.3),
                                       (917.0, 923.9),
                                       (917.2, 924.5),
                                   }
                                   select (Region, Hertz.Mega(freq.Input), dr, Hertz.Mega(freq.Output)));

        public static readonly TheoryData<Region, Hertz, DataRateIndex, Hertz> TestRegionFrequencyDataDR6 =
            TheoryDataFactory.From(from freq in new (Hertz Input, Hertz Output)[]
                                   {
                                       (Mega(915.9), Mega(923.3)),
                                       (Mega(917.5), Mega(923.9)),
                                       (Mega(919.1), Mega(924.5)),
                                       (Mega(920.7), Mega(925.1)),
                                       (Mega(922.3), Mega(925.7)),
                                       (Mega(923.9), Mega(926.3)),
                                       (Mega(925.5), Mega(926.9)),
                                       (Mega(927.1), Mega(927.5)),
                                   }
                                   select (Region, freq.Input, /* data rate */ DR6, freq.Output));

        public static TheoryData<Region, DataRateIndex, DataRateIndex> TestRegionDataRateDataDR0To5() =>
             TheoryDataFactory.From(new[]
             {
                 (Region, DR0, DR8),
                 (Region, DR1, DR9),
                 (Region, DR2, DR10),
                 (Region, DR3, DR11),
                 (Region, DR4, DR12),
                 (Region, DR5, DR13)
             });

        public static TheoryData<Region, DataRateIndex, DataRateIndex> TestRegionDataRateDataDR6() =>
            TheoryDataFactory.From(new[] { (Region, DR6, DR13) });

        public static TheoryData<Region, DataRateIndex, DataRateIndex> TestRegionDataRateData_InvalidOffset =>
           TheoryDataFactory.From(new[]
           {
               (Region, DR0, DR6),
               (Region, DR0, DR7),
           });

        public static TheoryData<Region, Hertz, DataRateIndex> TestRegionLimitData =>
            TheoryDataFactory.From(new (Region, Hertz, DataRateIndex)[]
            {
                (Region, Mega( 914.9), DR7),
                (Region, Mega(1024.0), DR8),
                (Region, Mega( 901.2), (DataRateIndex)90),
                (Region, Mega( 930.1), (DataRateIndex)100),
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
               (Region, DR8, 41),
               (Region, DR9, 117),
               (Region, DR10, 230),
               (Region, DR11, 230),
               (Region, DR12, 230),
               (Region, DR13, 230),
           });

        public static TheoryData<Region, Hertz?, Hertz> TestDownstreamRX2FrequencyData =>
           TheoryDataFactory.From(new (Region, Hertz? NwkSrvRx2Freq, Hertz ExpectedFreq)[]
           {
               (Region, null       , Mega(923.3)),
               (Region, Mega(920.0), Mega(920.0)),
           });

        public static TheoryData<Region, DataRateIndex?, DataRateIndex?, DataRateIndex> TestDownstreamRX2DataRateData =>
           TheoryDataFactory.From<Region, DataRateIndex?, DataRateIndex?, DataRateIndex>(new (Region, DataRateIndex?, DataRateIndex?, DataRateIndex)[]
           {
                (Region, null, null, DR8),
                (Region, DR11, null, DR11),
                (Region, DR11, DR12, DR12),
           });

        public static TheoryData<Region, LoRaRegionType> TestTranslateToRegionData =>
           TheoryDataFactory.From(new[]
           {
                (Region, LoRaRegionType.AU915),
           });

        public static TheoryData<Region, Hertz, int> TestTryGetJoinChannelIndexData =>
            TheoryDataFactory.From(from freq in new Hertz[] { Mega(902.3), Mega(927.5) }
                                   select (Region, freq, /* expected index */ -1));

        public static TheoryData<Region, int, bool> TestIsValidRX1DROffsetData =>
           TheoryDataFactory.From(new[]
           {
                (Region, 0, true),
                (Region, 3, true),
                (Region, 4, true),
                (Region, 5, true),
                (Region, 6, false),
           });

        public static TheoryData<Region, DataRateIndex, bool, bool> TestIsDRIndexWithinAcceptableValuesData =>
            TheoryDataFactory.From<Region, DataRateIndex, bool, bool>(new[]
            {
                (Region, DR0, true, true),
                (Region, DR2, true, true),
                (Region, DR4, true, true),
                (Region, DR6, true, true),
                (Region, DR2, false, false),
                (Region, DR5, false, false),
                (Region, DR7, true, false),
                (Region, DR10, false, true),
                (Region, DR13, false, true),
                (Region, DR12, true, false),
                (Region, DR14, true, false),
            });
    }
}
