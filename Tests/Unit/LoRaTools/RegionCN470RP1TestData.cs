// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using static LoRaWan.Metric;

    public static class RegionCN470RP1TestData
    {
        private static readonly Region region = RegionManager.CN470RP1;

        public static IEnumerable<object[]> TestRegionFrequencyData =>
            from f in new (Hertz Input, Hertz Output)[]
            {
                (Mega(470.3), Mega(500.3)),
                (Mega(471.5), Mega(501.5)),
                (Mega(473.3), Mega(503.3)),
                (Mega(475.9), Mega(505.9)),
                (Mega(477.7), Mega(507.7)),
                (Mega(478.1), Mega(508.1)),
                (Mega(479.7), Mega(509.7)),
                (Mega(479.9), Mega(500.3)),
                (Mega(480.1), Mega(500.5)),
                (Mega(484.1), Mega(504.5)),
                (Mega(489.3), Mega(509.7)),
            }
            select new object[] { region, f.Input, /* data rate */ 0, f.Output };

        public static IEnumerable<object[]> TestRegionDataRateData =>
           new List<object[]>
           {
               new object[] { region, 0, 0, 0 },
               new object[] { region, 1, 1, 0 },
               new object[] { region, 2, 2, 0 },
               new object[] { region, 5, 5, 0 },
               new object[] { region, 1, 0, 5 },
               new object[] { region, 2, 1, 1 },
               new object[] { region, 3, 1, 2 },
               new object[] { region, 3, 0, 3 },
               new object[] { region, 4, 2, 2 },
               new object[] { region, 5, 2, 3 },
           };

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
           new List<object[]>
           {
               new object[] { region, 0, 6 },
               new object[] { region, 2, 10 },
           };

        public static IEnumerable<object[]> TestRegionLimitData =>
          from x in new[]
          {
               new { Frequency = 467.0, DataRate =   6 },
               new { Frequency = 469.9, DataRate =   7 },
               new { Frequency = 510.8, DataRate =  20 },
               new { Frequency = 512.3, DataRate = 110 },
          }
          select new object[] { region, Hertz.Mega(x.Frequency), x.DataRate };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, 0, 59 },
               new object[] { region, 1, 59 },
               new object[] { region, 2, 59 },
               new object[] { region, 3, 123 },
               new object[] { region, 4, 230 },
               new object[] { region, 5, 230 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new[]
           {
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 505.3 },
               new { NwkSrvRx2Freq = 505.3     , ExpectedFreq = 505.3 },
               new { NwkSrvRx2Freq = 500.3     , ExpectedFreq = 500.3 },
               new { NwkSrvRx2Freq = 509.7     , ExpectedFreq = 509.7 },
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
                new object[] { region, null, null, (ushort)0 },
                new object[] { region, null, (ushort)2, (ushort)2 },
                new object[] { region, null, (ushort)5, (ushort)5 },
                new object[] { region, null, (ushort)6, (ushort)0 },
                new object[] { region, (ushort)4, null, (ushort)4 },
                new object[] { region, (ushort)4, (ushort)5, (ushort)5 },
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.CN470RP1 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from x in new[]
            {
                new { Freq = 470.3, ExpectedIndex = -1 },
                new { Freq = 489.3, ExpectedIndex = -1 },
                new { Freq = 509.7, ExpectedIndex = -1 },
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
                new object[] { region, 5, true },
                new object[] { region, 6, false },
           };

        public static IEnumerable<object[]> TestIsDRIndexWithinAcceptableValuesData =>
            new List<object[]>
            {
                new object[] { region, (ushort)0, true, true },
                new object[] { region, (ushort)2, true, true },
                new object[] { region, (ushort)5, true, true },
                new object[] { region, (ushort)6, true, false },
                new object[] { region, (ushort)0, false, true },
                new object[] { region, (ushort)2, false, true },
                new object[] { region, (ushort)5, false, true },
                new object[] { region, (ushort)6, false, false },
                new object[] { region, (ushort)10, false, false },
                new object[] { region, null, false, false },
            };
    }
}
