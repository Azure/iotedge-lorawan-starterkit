// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;

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
            from freq in new (double Input, double Output)[]
            {
                (903  , 923.3),
                (904.6, 923.9),
                (906.2, 924.5),
                (907.8, 925.1),
                (909.4, 925.7),
                (911  , 926.3),
                (912.6, 926.9),
                (914.2, 927.5),
            }
            select new object[] { region, Hertz.Mega(freq.Input), /* data rate */ 4, Hertz.Mega(freq.Output) };

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
          from x in new[]
          {
               new { Frequency =  700.0, DataRate =   5 },
               new { Frequency = 1024.0, DataRate =  10 },
               new { Frequency =  901.2, DataRate =  90 },
               new { Frequency =  928.5, DataRate = 100 },
          }
          select new object[] { region, Hertz.Mega(x.Frequency), x.DataRate };

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
           from x in new[]
           {
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 923.3 },
               new { NwkSrvRx2Freq = 920.0     , ExpectedFreq = 920.0 },
           }
           select new object[]
           {
               region,
               !double.IsNaN(x.NwkSrvRx2Freq) ? Hertz.Mega(x.NwkSrvRx2Freq) : (Hertz?)null,
               Hertz.Mega(x.ExpectedFreq)
           };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
           new List<object[]>
           {
                new object[] { region, null, null, (ushort)8 },
                new object[] { region, (ushort)11, null, (ushort)11 },
                new object[] { region, (ushort)11, (ushort)12, (ushort)12 },
           };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.US915 },
                new object[] { region, LoRaRegionType.US902 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from x in new[]
            {
                new { Freq = 902.3, ExpectedIndex = -1 },
                new { Freq = 927.5, ExpectedIndex = -1 },
            }
            select new object[]
            {
                region,
                Hertz.Mega(x.Freq),
                x.ExpectedIndex,
            };

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
                new object[] { region, (ushort)0, true, true },
                new object[] { region, (ushort)2, true, true },
                new object[] { region, (ushort)4, true, true },
                new object[] { region, (ushort)10, false, true },
                new object[] { region, (ushort)13, false, true },
                new object[] { region, (ushort)2, false, false },
                new object[] { region, (ushort)5, true, false },
                new object[] { region, (ushort)7, true, false },
                new object[] { region, (ushort)10, true, false },
                new object[] { region, (ushort)12, true, false },
                new object[] { region, (ushort)14, true, false },
                new object[] { region, null, false, false },
            };
    }
}
