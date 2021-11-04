// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System.Collections.Generic;
    using System.Linq;
    using LoRaTools.Regions;

    public static class RegionAS923TestData
    {
        private static readonly Region region = RegionManager.AS923;
        private static readonly List<DataRate> dataRates = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 }.Select(dr => new DataRate(dr)).ToList();
        private static readonly List<Hertz> frequencies = new List<ulong> { 923_200_000, 923_400_000 }.Select(fr => new Hertz(fr)).ToList();

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
                new object[] { region, freq, 0, 0, 0 },
                new object[] { region, freq, 1, 1, 0 },
                new object[] { region, freq, 2, 2, 0 },
                new object[] { region, freq, 6, 6, 0 },
                new object[] { region, freq, 2, 1, 1 },
                new object[] { region, freq, 3, 1, 2 },
                new object[] { region, freq, 4, 2, 2 },
                new object[] { region, freq, 6, 3, 3 },
                new object[] { region, freq, 6, 7, 6 },
                new object[] { region, freq, 3, 4, 6 },
                new object[] { region, freq, 1, 1, 10 },
            };
        }
    }
}
