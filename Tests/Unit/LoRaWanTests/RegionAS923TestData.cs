// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System.Collections.Generic;
    using System.Linq;
    using LoRaTools.Regions;

    public static class RegionAS923TestData
    {
        private static readonly List<DataRate> dataRates =
            new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 }
            .Select(dr => new DataRate(dr)).ToList();

        private static readonly List<Hertz> frequencies =
            new List<ulong> { 923_200_000, 923_400_000, 921_400_000, 916_600_000, 917_500_000 }
            .Select(fr => new Hertz(fr)).ToList();

        private static readonly Region region = new RegionAS923(frequencies[0], frequencies[1]);
        private static readonly Region regionWithDwellTime = new RegionAS923(frequencies[0], frequencies[1], 1);

        public static IEnumerable<object[]> TestRegionFrequencyData()
        {
            foreach (var dr in dataRates)
            {
                foreach (var freq in frequencies)
                    yield return new object[] { region, freq.Mega, dr.AsInt32, freq.Mega };
            }
        }

        public static IEnumerable<object[]> TestRegionDataRateData()
        {
            var freq = frequencies[0].Mega;
            return new List<object[]>
            {
                // No DwellTime limit
                new object[] { region, freq, 0, 0, 0 },
                new object[] { region, freq, 1, 1, 0 },
                new object[] { region, freq, 6, 6, 0 },
                new object[] { region, freq, 2, 1, 1 },
                new object[] { region, freq, 3, 1, 2 },
                new object[] { region, freq, 4, 2, 2 },
                new object[] { region, freq, 5, 7, 7 },
                new object[] { region, freq, 6, 7, 6 },
                new object[] { region, freq, 3, 4, 6 },
                new object[] { region, freq, 1, 1, 10 },
                // With DwellTime limit
                new object[] { regionWithDwellTime, freq, 0, 2, 0 },
                new object[] { regionWithDwellTime, freq, 1, 2, 0 },
                new object[] { regionWithDwellTime, freq, 6, 6, 0 },
                new object[] { regionWithDwellTime, freq, 2, 2, 1 },
                new object[] { regionWithDwellTime, freq, 3, 2, 2 },
                new object[] { regionWithDwellTime, freq, 4, 2, 2 },
                new object[] { regionWithDwellTime, freq, 5, 7, 7 },
                new object[] { regionWithDwellTime, freq, 6, 7, 6 },
                new object[] { regionWithDwellTime, freq, 3, 4, 6 },
                new object[] { regionWithDwellTime, freq, 1, 1, 10 },
            };
        }

        public static IEnumerable<object[]> TestRegionLimitData =>
          new List<object[]>
          {
               new object[] { region, 900, 0 },
               new object[] { region, 914.5, 4 },
               new object[] { region, 930, 4 },
               new object[] { region, 923.4, 18 },
               new object[] { region, 925.5, 90 },
               new object[] { region, 923.2, 100 },
          };

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
           new List<object[]>
           {
               new object[] { region, null, 923.2 },
               new object[] { region, 923.4, 923.4 },
               new object[] { region, 925, 925 },
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
            new List<object[]>
            {
                new object[] { region, 923.4, -1 },
                new object[] { region, 928, -1 },
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
