// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using global::LoRaTools.Regions;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public static class RegionCN470RP2TestData
    {
        private static readonly Region Region = RegionManager.CN470RP2;

        public static TheoryData<Region, Hertz, DataRateIndex, Hertz, int> TestRegionFrequencyData =>
            TheoryDataFactory.From<Region, Hertz, DataRateIndex, Hertz, int>(new (Region, Hertz, DataRateIndex, Hertz, int)[]
            {
                // 20 MHz plan A
                (Region, Mega(470.3), DR0, Mega(483.9), 0),
                (Region, Mega(471.5), DR0, Mega(485.1), 1),
                (Region, Mega(476.5), DR0, Mega(490.1), 2),
                (Region, Mega(503.9), DR0, Mega(490.7), 3),
                (Region, Mega(503.5), DR0, Mega(490.3), 4),
                (Region, Mega(504.5), DR0, Mega(491.3), 5),
                (Region, Mega(509.7), DR0, Mega(496.5), 7),
                // 20 MHz plan B
                (Region, Mega(476.9), DR0, Mega(476.9), 8),
                (Region, Mega(479.9), DR0, Mega(479.9), 8),
                (Region, Mega(503.1), DR0, Mega(503.1), 9),
                // 26 MHz plan A
                (Region, Mega(470.3), DR0, Mega(490.1), 10),
                (Region, Mega(473.3), DR0, Mega(493.1), 11),
                (Region, Mega(475.1), DR0, Mega(490.1), 12),
                (Region, Mega(471.1), DR0, Mega(490.9), 14),
                // 26 MHz plan B
                (Region, Mega(480.3), DR0, Mega(500.1), 15),
                (Region, Mega(485.1), DR0, Mega(500.1), 16),
                (Region, Mega(485.3), DR0, Mega(500.3), 17),
                (Region, Mega(489.7), DR0, Mega(504.7), 18),
                (Region, Mega(488.9), DR0, Mega(503.9), 19),
            });

        public static TheoryData<Region, DataRateIndex, DataRateIndex, int> TestRegionDataRateData =>
           TheoryDataFactory.From<Region, DataRateIndex, DataRateIndex, int>(new[]
           {
               (Region, DR0, DR0, 0),
               (Region, DR1, DR1, 0),
               (Region, DR2, DR2, 0),
               (Region, DR6, DR6, 0),
               (Region, DR2, DR1, 1),
               (Region, DR3, DR1, 2),
               (Region, DR4, DR2, 2),
               (Region, DR6, DR3, 3),
           });

        public static TheoryData<Region, DataRateIndex, int> TestRegionDataRateData_InvalidOffset =>
           TheoryDataFactory.From(new[]
           {
               (Region, DR0, 6),
               (Region, DR2, 10),
           });

        public static TheoryData<Region, Hertz, DataRateIndex, int> TestRegionLimitData =>
            TheoryDataFactory.From<Region, Hertz, DataRateIndex, int>(new (Region, Hertz, DataRateIndex, int)[]
            {
                (Region, Mega(470.0), DR8, 0),
                (Region, Mega(510.0), DR10, 0),
                (Region, Mega(509.8), (DataRateIndex)100, 0),
                (Region, Mega(469.9), (DataRateIndex)110, 0),
            });

        public static TheoryData<Region, DataRateIndex, uint> TestRegionMaxPayloadLengthData =>
           TheoryDataFactory.From(new (Region, DataRateIndex, uint)[]
           {
               (Region, DR1, 31),
               (Region, DR2, 94),
               (Region, DR3, 192),
               (Region, DR4, 250),
               (Region, DR5, 250),
               (Region, DR6, 250),
               (Region, DR7, 250),
           });

        public static TheoryData<Region, Hertz?, Hertz, int?, int?> TestDownstreamRX2FrequencyData =>
           TheoryDataFactory.From<Region, Hertz?, Hertz, int?, int?>(
               new (Region, Hertz?, Hertz, int?, int?)[]
               {
                   // OTAA devices
                   (Region, null,        Mega(485.3), 0,  null ),
                   (Region, null,        Mega(486.9), 1,  9    ),
                   (Region, null,        Mega(496.5), 7,  null ),
                   (Region, null,        Mega(498.3), 9,  8    ),
                   (Region, null,        Mega(492.5), 10, null ),
                   (Region, null,        Mega(492.5), 12, null ),
                   (Region, null,        Mega(492.5), 14, 14   ),
                   (Region, null,        Mega(502.5), 17, null ),
                   (Region, null,        Mega(502.5), 19, 18   ),
                   (Region, Mega(498.3), Mega(498.3), 7,  null ),
                   (Region, Mega(485.3), Mega(485.3), 15, null ),
                   (Region, Mega(492.5), Mega(492.5), 15, 15   ),
                   // ABP devices
                   (Region, null,        Mega(486.9), null, 0  ),
                   (Region, null,        Mega(486.9), null, 7  ),
                   (Region, null,        Mega(498.3), null, 8  ),
                   (Region, null,        Mega(498.3), null, 9  ),
                   (Region, null,        Mega(492.5), null, 14 ),
                   (Region, null,        Mega(502.5), null, 15 ),
                   (Region, null,        Mega(502.5), null, 19 ),
                   (Region, Mega(486.9), Mega(486.9), null, 12 ),
                   (Region, Mega(502.5), Mega(502.5), null, 17 ),
               });

        public static TheoryData<Region, DataRateIndex?, DataRateIndex?, DataRateIndex, int?, int?> TestDownstreamRX2DataRateData =>
            TheoryDataFactory.From<Region, DataRateIndex?, DataRateIndex?, DataRateIndex, int?, int?>(new (Region, DataRateIndex?, DataRateIndex?, DataRateIndex, int?, int?)[]
            {
                (Region, null, null, DR1, 0, null),
                (Region, null, null, DR1, 8, null),
                (Region, null, null, DR1, 10, null),
                (Region, null, null, DR1, 19, null),
                (Region, null, null, DR1, null, 5),
                (Region, null, null, DR1, null, 12),
                (Region, null, null, DR1, 10, 14),
                (Region, null, DR2 , DR2, 0, null),
                (Region, DR3 , null, DR3, 0, null),
                (Region, DR3 , DR2 , DR2, 0, null),
                (Region, DR4 , DR3 , DR3, 0, 8),
                (Region, null, DR9 , DR1, 11, null),
            });

        public static TheoryData<Region, LoRaRegionType> TestTranslateToRegionData =>
           TheoryDataFactory.From(new[] { (Region, LoRaRegionType.CN470RP2) });

        public static TheoryData<Region, Hertz, int> TestTryGetJoinChannelIndexData =>
            TheoryDataFactory.From(new (Region, Hertz, int)[]
            {
                (Region, Mega(470.9), 0),
                (Region, Mega(472.5), 1),
                (Region, Mega(475.7), 3),
                (Region, Mega(507.3), 6),
                (Region, Mega(479.9), 8),
                (Region, Mega(499.9), 9),
                (Region, Mega(478.3), 14),
                (Region, Mega(482.3), 16),
                (Region, Mega(486.3), 18),
                (Region, Mega(488.3), 19),
            });

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
                (Region, DR7, true, true),
                (Region, DR0, false, true),
                (Region, DR2, false, true),
                (Region, DR7, false, true),
                (Region, DR9, true, false),
                (Region, DR10, false, false),
            });
    }
}
