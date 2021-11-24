// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using global::LoRaTools.Regions;

    public static class RegionEU868TestData
    {
        private static readonly Region region = RegionManager.EU868;
        private static readonly List<ushort> dataRates = new List<ushort> { 0, 1, 2, 3, 4, 5, 6 };
        private static readonly List<double> frequencies = new List<double> { 868.1, 868.3, 868.5 };

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
          new List<object[]>
          {
               new object[] { region, 800, 8 },
               new object[] { region, 1023, 10 },
               new object[] { region, 862.1, 90 },
               new object[] { region, 860.3, 100 },
               new object[] { region, 880, 100 },
          };

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
           new List<object[]>
           {
               new object[] { region, null, 869.525 },
               new object[] { region, 868.250, 868.250 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { region, null, null, (ushort)0 }, // Standard EU.
                new object[] { region, (ushort)3, null, (ushort)3 }, // nwksrvDR is correctly applied if no device twins.
                new object[] { region, (ushort)3, (ushort)6, (ushort)6 }, // device twins are applied in priority.
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.EU868 },
                new object[] { region, LoRaRegionType.EU863 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            new List<object[]>
            {
                new object[] { region, 863, -1 },
                new object[] { region, 870, -1 },
            };

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
                new object[] { region, (ushort)0, true, true },
                new object[] { region, (ushort)1, true, true },
                new object[] { region, (ushort)3, true, true },
                new object[] { region, (ushort)5, false, true },
                new object[] { region, (ushort)8, true, false },
                new object[] { region, (ushort)8, false, false },
                new object[] { region, (ushort)10, true, false },
                new object[] { region, (ushort)10, false, false },
                new object[] { region, null, false, false },
            };
    }
}
