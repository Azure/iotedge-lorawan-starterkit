// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools.Regions;
    using static LoRaWan.Metric;

    public static class RegionCN470RP2TestData
    {
        private static readonly Region region = RegionManager.CN470RP2;

        public static readonly IEnumerable<object[]> TestRegionFrequencyData =
            from p in new[]
            {
                // 20 MHz plan A
                new { Frequency = new { Input = Mega(470.3), Output = Mega(483.9) }, JoinChannel = 0 },
                new { Frequency = new { Input = Mega(471.5), Output = Mega(485.1) }, JoinChannel = 1 },
                new { Frequency = new { Input = Mega(476.5), Output = Mega(490.1) }, JoinChannel = 2 },
                new { Frequency = new { Input = Mega(503.9), Output = Mega(490.7) }, JoinChannel = 3 },
                new { Frequency = new { Input = Mega(503.5), Output = Mega(490.3) }, JoinChannel = 4 },
                new { Frequency = new { Input = Mega(504.5), Output = Mega(491.3) }, JoinChannel = 5 },
                new { Frequency = new { Input = Mega(509.7), Output = Mega(496.5) }, JoinChannel = 7 },
                // 20 MHz plan B
                new { Frequency = new { Input = Mega(476.9), Output = Mega(476.9) }, JoinChannel = 8 },
                new { Frequency = new { Input = Mega(479.9), Output = Mega(479.9) }, JoinChannel = 8 },
                new { Frequency = new { Input = Mega(503.1), Output = Mega(503.1) }, JoinChannel = 9 },
                // 26 MHz plan A
                new { Frequency = new { Input = Mega(470.3), Output = Mega(490.1) }, JoinChannel = 10 },
                new { Frequency = new { Input = Mega(473.3), Output = Mega(493.1) }, JoinChannel = 11 },
                new { Frequency = new { Input = Mega(475.1), Output = Mega(490.1) }, JoinChannel = 12 },
                new { Frequency = new { Input = Mega(471.1), Output = Mega(490.9) }, JoinChannel = 14 },
                // 26 MHz plan B
                new { Frequency = new { Input = Mega(480.3), Output = Mega(500.1) }, JoinChannel = 15 },
                new { Frequency = new { Input = Mega(485.1), Output = Mega(500.1) }, JoinChannel = 16 },
                new { Frequency = new { Input = Mega(485.3), Output = Mega(500.3) }, JoinChannel = 17 },
                new { Frequency = new { Input = Mega(489.7), Output = Mega(504.7) }, JoinChannel = 18 },
                new { Frequency = new { Input = Mega(488.9), Output = Mega(503.9) }, JoinChannel = 19 },
            }
            select new object[] { region, p.Frequency.Input, /* data rate */ 0, p.Frequency.Output, p.JoinChannel };

        public static IEnumerable<object[]> TestRegionDataRateData =>
           new List<object[]>
           {
               new object[] { region, 0, 0, 0 },
               new object[] { region, 1, 1, 0 },
               new object[] { region, 2, 2, 0 },
               new object[] { region, 6, 6, 0 },
               new object[] { region, 2, 1, 1 },
               new object[] { region, 3, 1, 2 },
               new object[] { region, 4, 2, 2 },
               new object[] { region, 6, 3, 3 },
           };

        public static IEnumerable<object[]> TestRegionDataRateData_InvalidOffset =>
           new List<object[]>
           {
               new object[] { region, 0, 6 },
               new object[] { region, 2, 10 },
           };

        public static IEnumerable<object[]> TestRegionLimitData =>
           new List<object[]>
           {
               new object[] { region, Mega(470), 8, 0 },
               new object[] { region, Mega(510), 10, 0 },
               new object[] { region, Mega(509.8), 100, 0 },
               new object[] { region, Mega(469.9), 110, 0 },
           };

        public static IEnumerable<object[]> TestRegionMaxPayloadLengthData =>
           new List<object[]>
           {
               new object[] { region, 1, 31 },
               new object[] { region, 2, 94 },
               new object[] { region, 3, 192 },
               new object[] { region, 4, 250 },
               new object[] { region, 5, 250 },
               new object[] { region, 6, 250 },
               new object[] { region, 7, 250 },
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           from x in new[]
           {
               // OTAA devices
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 485.3, JoinChannel = new { Reported = (int?) 0, Desired = (int?)null } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 486.9, JoinChannel = new { Reported = (int?) 1, Desired = (int?)9    } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 496.5, JoinChannel = new { Reported = (int?) 7, Desired = (int?)null } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 498.3, JoinChannel = new { Reported = (int?) 9, Desired = (int?)8    } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 492.5, JoinChannel = new { Reported = (int?)10, Desired = (int?)null } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 492.5, JoinChannel = new { Reported = (int?)12, Desired = (int?)null } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 492.5, JoinChannel = new { Reported = (int?)14, Desired = (int?)14   } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 502.5, JoinChannel = new { Reported = (int?)17, Desired = (int?)null } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 502.5, JoinChannel = new { Reported = (int?)19, Desired = (int?)18   } },
               new { NwkSrvRx2Freq = 498.3     , ExpectedFreq = 498.3, JoinChannel = new { Reported = (int?) 7, Desired = (int?)null } },
               new { NwkSrvRx2Freq = 485.3     , ExpectedFreq = 485.3, JoinChannel = new { Reported = (int?)15, Desired = (int?)null } },
               new { NwkSrvRx2Freq = 492.5     , ExpectedFreq = 492.5, JoinChannel = new { Reported = (int?)15, Desired = (int?)15   } },
               // ABP devices
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 486.9, JoinChannel = new { Reported = (int?)null, Desired = (int?)0  } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 486.9, JoinChannel = new { Reported = (int?)null, Desired = (int?)7  } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 498.3, JoinChannel = new { Reported = (int?)null, Desired = (int?)8  } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 498.3, JoinChannel = new { Reported = (int?)null, Desired = (int?)9  } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 492.5, JoinChannel = new { Reported = (int?)null, Desired = (int?)14 } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 502.5, JoinChannel = new { Reported = (int?)null, Desired = (int?)15 } },
               new { NwkSrvRx2Freq = double.NaN, ExpectedFreq = 502.5, JoinChannel = new { Reported = (int?)null, Desired = (int?)19 } },
               new { NwkSrvRx2Freq = 486.9     , ExpectedFreq = 486.9, JoinChannel = new { Reported = (int?)null, Desired = (int?)12 } },
               new { NwkSrvRx2Freq = 502.5     , ExpectedFreq = 502.5, JoinChannel = new { Reported = (int?)null, Desired = (int?)17 } },
           }
           select new object[]
           {
               region,
               !double.IsNaN(x.NwkSrvRx2Freq) ? Hertz.Mega(x.NwkSrvRx2Freq) : (Hertz?)null,
               Hertz.Mega(x.ExpectedFreq),
               x.JoinChannel.Reported,
               x.JoinChannel.Desired,
           };

        public static IEnumerable<object[]> TestDownstreamRX2DataRateData =>
            new List<object[]>
            {
                new object[] { region, null, null, 1, 0, null },
                new object[] { region, null, null, 1, 8, null },
                new object[] { region, null, null, 1, 10, null },
                new object[] { region, null, null, 1, 19, null },
                new object[] { region, null, null, 1, null, 5 },
                new object[] { region, null, null, 1, null, 12 },
                new object[] { region, null, null, 1, 10, 14 },
                new object[] { region, null, (ushort)2, 2, 0, null },
                new object[] { region, (ushort)3, null, 3, 0, null },
                new object[] { region, (ushort)3, (ushort)2, 2, 0, null },
                new object[] { region, (ushort)4, (ushort)3, 3, 0, 8 },
                new object[] { region, null, (ushort)9, 1, 11, null },
            };

        public static IEnumerable<object[]> TestTranslateToRegionData =>
           new List<object[]>
           {
                new object[] { region, LoRaRegionType.CN470RP2 },
           };

        public static IEnumerable<object[]> TestTryGetJoinChannelIndexData =>
            from x in new[]
            {
                new { Freq = 470.9, ExpectedIndex =  0 },
                new { Freq = 472.5, ExpectedIndex =  1 },
                new { Freq = 475.7, ExpectedIndex =  3 },
                new { Freq = 507.3, ExpectedIndex =  6 },
                new { Freq = 479.9, ExpectedIndex =  8 },
                new { Freq = 499.9, ExpectedIndex =  9 },
                new { Freq = 478.3, ExpectedIndex = 14 },
                new { Freq = 482.3, ExpectedIndex = 16 },
                new { Freq = 486.3, ExpectedIndex = 18 },
                new { Freq = 488.3, ExpectedIndex = 19 },
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
