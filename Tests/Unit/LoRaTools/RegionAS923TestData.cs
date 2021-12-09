// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using static LoRaWan.Metric;

    public static class RegionAS923TestData
    {
        private static readonly List<DataRate> dataRates =
            new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 }
            .Select(dr => new DataRate(dr)).ToList();

        private static readonly List<Hertz> frequencies =
            new List<ulong> { 923_200_000, 923_400_000, 921_400_000, 916_600_000, 917_500_000 }
            .Select(fr => new Hertz(fr)).ToList();

        private static readonly Region region = new RegionAS923().WithFrequencyOffset(frequencies[0], frequencies[1]);
        private static readonly Region regionWithDwellTime = new RegionAS923(1).WithFrequencyOffset(frequencies[0], frequencies[1]);

        public static IEnumerable<object[]> TestRegionFrequencyData()
        {
            foreach (var dr in dataRates)
            {
                foreach (var freq in frequencies)
                    yield return new object[] { region, freq, dr.AsInt32, freq };
            }
        }

        public static IEnumerable<object[]> TestRegionDataRateData() =>
            new List<object[]>
            {
                // No DwellTime limit
                new object[] { region, 0, 0, 0 },
                new object[] { region, 1, 1, 0 },
                new object[] { region, 6, 6, 0 },
                new object[] { region, 2, 1, 1 },
                new object[] { region, 3, 1, 2 },
                new object[] { region, 4, 2, 2 },
                new object[] { region, 5, 7, 7 },
                new object[] { region, 6, 7, 6 },
                new object[] { region, 3, 4, 6 },
                // With DwellTime limit
                new object[] { regionWithDwellTime, 0, 2, 0 },
                new object[] { regionWithDwellTime, 1, 2, 0 },
                new object[] { regionWithDwellTime, 6, 6, 0 },
                new object[] { regionWithDwellTime, 2, 2, 1 },
                new object[] { regionWithDwellTime, 3, 2, 2 },
                new object[] { regionWithDwellTime, 4, 2, 2 },
                new object[] { regionWithDwellTime, 5, 7, 7 },
                new object[] { regionWithDwellTime, 6, 7, 6 },
                new object[] { regionWithDwellTime, 3, 4, 6 },
            };

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
           new List<object[]>
           {
               new object[] { region, 1, 8 },
               new object[] { region, 1, 9 },
               new object[] { regionWithDwellTime, 1, 10 },
           };

        public static readonly IEnumerable<object[]> TestRegionLimitData =
          from x in new[]
          {
               new { Frequency = 900.0, DataRate =   8 },
               new { Frequency = 914.5, DataRate =   9 },
               new { Frequency = 930.0, DataRate =  10 },
               new { Frequency = 928.4, DataRate =  18 },
               new { Frequency = 928.5, DataRate =  90 },
               new { Frequency = 928.2, DataRate = 100 },
          }
          select new object[] { region, Hertz.FromMega(x.Frequency), x.DataRate };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, 0, 59 },
               new object[] { region, 1, 59 },
               new object[] { region, 2, 123 },
               new object[] { region, 3, 123 },
               new object[] { region, 4, 230 },
               new object[] { region, 5, 230 },
               new object[] { region, 6, 230 },
               new object[] { region, 7, 230 },
               new object[] { regionWithDwellTime, 2, 19 },
               new object[] { regionWithDwellTime, 3, 61 },
               new object[] { regionWithDwellTime, 4, 133 },
               new object[] { regionWithDwellTime, 5, 230 },
               new object[] { regionWithDwellTime, 6, 230 },
               new object[] { regionWithDwellTime, 7, 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new[]
           {
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 923.2 },
               new { NwkSrvRx2Freq = 923.4     , ExpectedFreq = 923.4 },
               new { NwkSrvRx2Freq = 925.0     , ExpectedFreq = 925.0 },
           }
           select new object[]
           {
               region,
               !double.IsNaN(x.NwkSrvRx2Freq) ? Hertz.FromMega(x.NwkSrvRx2Freq) : (Hertz?)null,
               Hertz.FromMega(x.ExpectedFreq)
           };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { region, null, null, 2 },
                new object[] { region, null, (ushort)2, 2 },
                new object[] { region, null, (ushort)5, 5 },
                new object[] { region, (ushort)3, null, 3 },
                new object[] { region, (ushort)3, (ushort)4, 4 },
                new object[] { region, (ushort)2, (ushort)3, 3 },
                new object[] { region, null, (ushort)9, 2 },
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.AS923 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from x in new[]
            {
                new { Freq = 923.4, ExpectedIndex = -1 },
                new { Freq = 928.0, ExpectedIndex = -1 },
            }
            select new object[]
            {
                region,
                Hertz.FromMega(x.Freq),
                x.ExpectedIndex,
            };

        public static IEnumerable<object[]> TestIsValidRX1DROffsetData =>
           new List<object[]>
           {
                new object[] { region, 0, true },
                new object[] { region, 7, true },
                new object[] { region, 8, false },
                new object[] { region, 10, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { region, (ushort)0, true, true },
                new object[] { region, (ushort)2, true, true },
                new object[] { region, (ushort)7, true, true },
                new object[] { region, (ushort)0, false, true },
                new object[] { region, (ushort)2, false, true },
                new object[] { region, (ushort)7, false, true },
                new object[] { region, (ushort)9, true, false },
                new object[] { region, (ushort)10, false, false },
                new object[] { region, null, false, false },
            };
    }
}
