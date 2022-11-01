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

    public static class RegionUS915TestData
    {
        private static readonly Region Region = RegionManager.US915;

        public static readonly TheoryData<Region, Hertz, DataRateIndex, Hertz> TestRegionFrequencyDataDR0To3 =
            TheoryDataFactory.From(from dr in new[] { DR0, DR1, DR2, DR3 }
                                   from freq in new (double Input, double Output)[]
                                   {
                                       (902.3, 923.3),
                                       (902.5, 923.9),
                                       (902.7, 924.5),
                                       (902.9, 925.1),
                                       (903.1, 925.7),
                                       (903.3, 926.3),
                                       (903.5, 926.9),
                                       (903.7, 927.5),
                                       (903.9, 923.3),
                                       (904.1, 923.9),
                                       (904.3, 924.5),
                                   }
                                   select (Region, Hertz.Mega(freq.Input), dr, Hertz.Mega(freq.Output)));

        public static readonly TheoryData<Region, Hertz, DataRateIndex, Hertz> TestRegionFrequencyDataDR4 =
            TheoryDataFactory.From(from freq in new (Hertz Input, Hertz Output)[]
                                   {
                                       (Mega(903  ), Mega(923.3)),
                                       (Mega(904.6), Mega(923.9)),
                                       (Mega(906.2), Mega(924.5)),
                                       (Mega(907.8), Mega(925.1)),
                                       (Mega(909.4), Mega(925.7)),
                                       (Mega(911  ), Mega(926.3)),
                                       (Mega(912.6), Mega(926.9)),
                                       (Mega(914.2), Mega(927.5)),
                                   }
                                   select (Region, freq.Input, /* data rate */ DR4, freq.Output));

        public static TheoryData<Region, DataRateIndex, DataRateIndex> TestRegionDataRateDataDR0To3() =>
             TheoryDataFactory.From(new[]
             {
                 (Region, DR0, DR10),
                 (Region, DR1, DR11),
                 (Region, DR2, DR12),
                 (Region, DR3, DR13)
             });

        public static TheoryData<Region, DataRateIndex, DataRateIndex> TestRegionDataRateDataDR4() =>
            TheoryDataFactory.From(new[] { (Region, DR4, DR13) });

        public static TheoryData<Region, DataRateIndex, DataRateIndex> TestRegionDataRateData_InvalidOffset =>
           TheoryDataFactory.From(new[]
           {
               (Region, DR0, DR4),
               (Region, DR0, DR5),
           });

        public static TheoryData<Region, Hertz, DataRateIndex> TestRegionLimitData =>
            TheoryDataFactory.From(new (Region, Hertz, DataRateIndex)[]
            {
                (Region, Mega( 700.0), DR5),
                (Region, Mega(1024.0), DR10),
                (Region, Mega( 901.2), (DataRateIndex)90),
                (Region, Mega( 928.5), (DataRateIndex)100),
            });

        public static TheoryData<Region, DataRateIndex, uint> TestRegionMaxPayloadLengthData =>
           TheoryDataFactory.From(new (Region, DataRateIndex, uint)[]
           {
               (Region, DR0, 19),
               (Region, DR1, 61),
               (Region, DR2, 133),
               (Region, DR3, 250),
               (Region, DR4, 250),
               (Region, DR8, 61),
               (Region, DR9, 137),
               (Region, DR10, 250),
               (Region, DR11, 250),
               (Region, DR13, 250),
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
                (Region, LoRaRegionType.US915),
                (Region, LoRaRegionType.US902),
           });

        public static TheoryData<Region, Hertz, int> TestTryGetJoinChannelIndexData =>
            TheoryDataFactory.From(from freq in new Hertz[] { Mega(902.3), Mega(927.5) }
                                   select (Region, freq, /* expected index */ -1));

        public static TheoryData<Region, int, bool> TestIsValidRX1DROffsetData =>
           TheoryDataFactory.From(new[]
           {
                (Region, 0, true),
                (Region, 3, true),
                (Region, 4, false),
           });

        public static TheoryData<Region, DataRateIndex, bool, bool> TestIsDRIndexWithinAcceptableValuesData =>
            TheoryDataFactory.From<Region, DataRateIndex, bool, bool>(new[]
            {
                (Region, DR0, true, true),
                (Region, DR2, true, true),
                (Region, DR4, true, true),
                (Region, DR10, false, true),
                (Region, DR13, false, true),
                (Region, DR2, false, false),
                (Region, DR5, true, false),
                (Region, DR7, true, false),
                (Region, DR10, true, false),
                (Region, DR12, true, false),
                (Region, DR14, true, false),
            });
    }
}
