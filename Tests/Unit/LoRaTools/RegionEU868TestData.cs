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
        private static readonly Region region = RegionManager.EU868;
        private static readonly ushort[] dataRates = { 0, 1, 2, 3, 4, 5, 6 };
        private static readonly Hertz[] frequencies = { Mega(868.1), Mega(868.3), Mega(868.5) };

        public static IEnumerable<object[]> TestRegionFrequencyData()
        {
            foreach (var dr in dataRates)
            {
                foreach (var freq in frequencies)
                    yield return new object[] { region, freq, dr, freq };
            }
        }

        public static IEnumerable<object[]> TestRegionDataRateData() {
            foreach (var dr in dataRates)
                yield return new object[] { region, dr, dr };
        }

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
          new List<object[]>
          {
               new object[] { region, 0, 6 },
               new object[] { region, 1, 10 },
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
            select new object[] { region, x.Frequency, x.DataRate };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, 0, 59 },
               new object[] { region, 1, 59 },
               new object[] { region, 2, 59 },
               new object[] { region, 3, 123 },
               new object[] { region, 4, 230 },
               new object[] { region, 5, 230 },
               new object[] { region, 6, 230 },
               new object[] { region, 7, 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new (Hertz? NwkSrvRx2Freq, Hertz ExpectedFreq)[]
           {
               (null         , Mega(869.525)),
               (Mega(868.250), Mega(868.250)),
           }
           select new object[] { region, x.NwkSrvRx2Freq, x.ExpectedFreq };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { region, null, null, DR0 }, // Standard EU.
                new object[] { region, DR3, null, DR3 }, // nwksrvDR is correctly applied if no device twins.
                new object[] { region, DR3, DR6, DR6 }, // device twins are applied in priority.
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.EU868 },
                new object[] { region, LoRaRegionType.EU863 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from freq in new Hertz[] { Mega(863), Mega(870) }
            select new object[] { region, freq, /* expected index */ -1 };

        public static IEnumerable<object[]> TestIsValidRX1DROffsetData =>
           new List<object[]>
           {
                new object[] { region, 0, true },
                new object[] { region, 5, true },
                new object[] { region, 6, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { region, DR0, true, true },
                new object[] { region, DR1, true, true },
                new object[] { region, DR3, true, true },
                new object[] { region, DR5, false, true },
                new object[] { region, DR8, true, false },
                new object[] { region, DR8, false, false },
                new object[] { region, DR10, true, false },
                new object[] { region, DR10, false, false },
                new object[] { region, null, false, false },
            };
    }
}
