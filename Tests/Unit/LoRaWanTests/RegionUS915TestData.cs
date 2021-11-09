// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using global::LoRaTools.Regions;

    public static class RegionUS915TestData
    {
        private static readonly Region region = RegionManager.US915;
        private static readonly List<double> frequenciesDR1To3 = new List<double> { 902.3, 902.5, 902.7, 902.9, 903.1, 903.3, 903.5, 903.7, 903.9 };
        private static readonly List<double> frequenciesDR4 = new List<double> { 903, 904.6, 906.2, 907.8, 909.4, 911, 912.6, 914.2 };

        public static IEnumerable<object[]> TestRegionFrequencyDataDR1To3()
        {
            var dataRates = new List<ushort> { 0, 1, 2, 3 };

            var inputFreqToExpectedFreq = new Dictionary<double, double>
            {
                { 902.3, 923.3 },
                { 902.5, 923.9 },
                { 902.7, 924.5 },
                { 902.9, 925.1 },
                { 903.1, 925.7 },
                { 903.3, 926.3 },
                { 903.5, 926.9 },
                { 903.7, 927.5 },
                { 903.9, 923.3 },
                { 904.1, 923.9 },
                { 904.3, 924.5 },
            };

            foreach (var dr in dataRates)
            {
                foreach (var freq in frequenciesDR1To3)
                    yield return new object[] { region, freq, dr, inputFreqToExpectedFreq[freq] };
            }
        }

        public static IEnumerable<object[]> TestRegionFrequencyDataDR4()
        {
            ushort dataRate = 4;

            var inputFreqToExpectedFreq = new Dictionary<double, double>
            {
                { 903,   923.3 },
                { 904.6, 923.9 },
                { 906.2, 924.5 },
                { 907.8, 925.1 },
                { 909.4, 925.7 },
                { 911,   926.3 },
                { 912.6, 926.9 },
                { 914.2, 927.5 },
            };

            foreach (var freq in frequenciesDR4)
                yield return new object[] { region, freq, dataRate, inputFreqToExpectedFreq[freq] };
        }

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
            {
                foreach (var freq in frequenciesDR1To3)
                    yield return new object[] { region, freq, dr, inputDrToExpectedDr[dr] };
            }
        }

        public static IEnumerable<object[]> TestRegionDataRateDataDR4()
        {
            ushort dataRate =  4;
            ushort expectedDr = 13;

            foreach (var freq in frequenciesDR4)
                yield return new object[] { region, freq, dataRate, expectedDr };
        }

        public static IEnumerable<object[]> TestRegionLimitData =>
          new List<object[]>
          {
               new object[] { region, 700, 0 },
               new object[] { region, 1024, 2 },
               new object[] { region, 915, 90 },
               new object[] { region, 920, 100 },
          };

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
           new List<object[]>
           {
               new object[] { region, null, 923.3, },
               new object[] { region, null, 923.3, },
               new object[] { region, 920.0, 920.0 },
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
            new List<object[]>
            {
                new object[] { region, 902.3, -1 },
                new object[] { region, 927.5, -1 },
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
