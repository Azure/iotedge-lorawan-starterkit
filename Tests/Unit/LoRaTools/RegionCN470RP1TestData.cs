// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using global::LoRaTools.Regions;

    public static class RegionCN470RP1TestData
    {
        private static readonly Region region = RegionManager.CN470RP1;

        public static IEnumerable<object[]> TestRegionFrequencyData()
        {
            var dataRate = 0;

            return new List<object[]>
            {
                new object[] { region, 470.3, dataRate, 500.3 },
                new object[] { region, 471.5, dataRate, 501.5 },
                new object[] { region, 473.3, dataRate, 503.3 },
                new object[] { region, 475.9, dataRate, 505.9 },
                new object[] { region, 477.7, dataRate, 507.7 },
                new object[] { region, 478.1, dataRate, 508.1 },
                new object[] { region, 479.7, dataRate, 509.7 },
                new object[] { region, 479.9, dataRate, 500.3 },
                new object[] { region, 480.1, dataRate, 500.5 },
                new object[] { region, 484.1, dataRate, 504.5 },
                new object[] { region, 489.3, dataRate, 509.7 },
            };
        }

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
           new List<object[]>
           {
               new object[] { region, 467, 6 },
               new object[] { region, 469.9, 7 },
               new object[] { region, 510.8, 20 },
               new object[] { region, 512.3, 110 },
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
           };

        public static IEnumerable<object[]> TestDownstreamRX2FrequencyData =>
           new List<object[]>
           {
               new object[] { region, null, 505.3 },
               new object[] { region, 505.3, 505.3 },
               new object[] { region, 500.3, 500.3 },
               new object[] { region, 509.7, 509.7 },
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
           new List<object[]>
           {
                new object[] { region, 470.3, -1 },
                new object[] { region, 489.3, -1 },
                new object[] { region, 509.7, -1 },
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
