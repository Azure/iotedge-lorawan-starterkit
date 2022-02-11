// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public static class RegionEU868TestData
    {
        private static readonly Region Region = RegionManager.EU868;
        private static readonly ushort[] DataRates = { 0, 1, 2, 3, 4, 5, 6 };
        private static readonly Hertz[] Frequencies = { Mega(868.1), Mega(868.3), Mega(868.5) };

        public static IEnumerable<object[]> TestRegionFrequencyData()
        {
            foreach (var dr in DataRates)
            {
                foreach (var freq in Frequencies)
                    yield return new object[] { Region, freq, dr, freq };
            }
        }

        public static IEnumerable<object[]> TestRegionDataRateData() {
            foreach (var dr in DataRates)
                yield return new object[] { Region, dr, dr };
        }

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
          new List<object[]>
          {
               new object[] { Region, 0, 6 },
               new object[] { Region, 1, 10 },
          };

        public static IEnumerable<object[]> TestRegionLimitData =>
            from x in new(Hertz Frequency, ushort DataRate)[]
            {
                (Mega( 800  ),   8),
                (Mega(1023  ),  10),
                (Mega( 862.1),  90),
                (Mega( 860.3), 100),
                (Mega( 880  ), 100),
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
               new object[] { Region, 6, 230 },
               new object[] { Region, 7, 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new (Hertz? NwkSrvRx2Freq, Hertz ExpectedFreq)[]
           {
               (null         , Mega(869.525)),
               (Mega(868.250), Mega(868.250)),
           }
           select new object[] { Region, x.NwkSrvRx2Freq, x.ExpectedFreq };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { Region, null, null, DR0 }, // Standard EU.
                new object[] { Region, DR3, null, DR3 }, // nwksrvDR is correctly applied if no device twins.
                new object[] { Region, DR3, DR6, DR6 }, // device twins are applied in priority.
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { Region, LoRaRegionType.EU868 },
                new object[] { Region, LoRaRegionType.EU863 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from freq in new Hertz[] { Mega(863), Mega(870) }
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
                new object[] { Region, DR1, true, true },
                new object[] { Region, DR3, true, true },
                new object[] { Region, DR5, false, true },
                new object[] { Region, DR8, true, false },
                new object[] { Region, DR8, false, false },
                new object[] { Region, DR10, true, false },
                new object[] { Region, DR10, false, false },
            };
    }
}
