// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public static class RegionUS915TestData
    {
        private static readonly Region region = RegionManager.US915;

        public static readonly IEnumerable<object[]> TestRegionFrequencyDataDR1To3 =
            from dr in new ushort[] { 0, 1, 2, 3 }
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
            select new object[] { region, Hertz.Mega(freq.Input), dr, Hertz.Mega(freq.Output) };

        public static readonly IEnumerable<object[]> TestRegionFrequencyDataDR4 =
            from freq in new (Hertz Input, Hertz Output)[]
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
            select new object[] { region, freq.Input, /* data rate */ 4, freq.Output };

        public static IEnumerable<object[]> TestRegionDataRateDataDR1To3()
        {
            var dataRates = new List<ushort> { 0, 1, 2, 3 };

            var inputDrToExpectedDr = new Dictionary<ushort, ushort>
            {
                { 0, 10 },
                { 1, 11 },
                { 2, 12 },
                { 3, 13 }
            };

            foreach (var dr in dataRates)
                yield return new object[] { region, dr, inputDrToExpectedDr[dr] };
        }

        public static IEnumerable<object[]> TestRegionDataRateDataDR4() =>
            new List<object[]>
            {
                new object[]{ region, 4, 13 }
            };

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
           new List<object[]>
           {
               new object[] { region, 0, 4 },
               new object[] { region, 0, 5 },
           };

        public static IEnumerable<object[]> TestRegionLimitData =>
            from x in new(Hertz Frequency, ushort DataRate)[]
            {
                (Mega( 700.0),   5),
                (Mega(1024.0),  10),
                (Mega( 901.2),  90),
                (Mega( 928.5), 100),
            }
            select new object[] { region, x.Frequency, x.DataRate };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, 0, 19 },
               new object[] { region, 1, 61 },
               new object[] { region, 2, 133 },
               new object[] { region, 3, 250 },
               new object[] { region, 4, 250 },
               new object[] { region, 8, 61 },
               new object[] { region, 9, 137 },
               new object[] { region, 10, 250 },
               new object[] { region, 11, 250 },
               new object[] { region, 13, 250 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new (Hertz? NwkSrvRx2Freq, Hertz ExpectedFreq)[]
           {
               (null       , Mega(923.3)),
               (Mega(920.0), Mega(920.0)),
           }
           select new object[] { region, x.NwkSrvRx2Freq, x.ExpectedFreq };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
           new List<object[]>
           {
                new object[] { region, null, null, DR8 },
                new object[] { region, DR11, null, DR11 },
                new object[] { region, DR11, DR12, DR12 },
           };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.US915 },
                new object[] { region, LoRaRegionType.US902 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from freq in new Hertz[] { Mega(902.3), Mega(927.5) }
            select new object[] { region, freq, /* expected index */ -1 };

        public static IEnumerable<object[]> TestIsValidRX1DROffsetData =>
           new List<object[]>
           {
                new object[] { region, 0, true },
                new object[] { region, 3, true },
                new object[] { region, 4, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { region, DR0, true, true },
                new object[] { region, DR2, true, true },
                new object[] { region, DR4, true, true },
                new object[] { region, DR10, false, true },
                new object[] { region, DR13, false, true },
                new object[] { region, DR2, false, false },
                new object[] { region, DR5, true, false },
                new object[] { region, DR7, true, false },
                new object[] { region, DR10, true, false },
                new object[] { region, DR12, true, false },
                new object[] { region, DR14, true, false },
                new object[] { region, null, false, false },
            };
    }
}
