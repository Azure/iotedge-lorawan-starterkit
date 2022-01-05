// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Regions
{
    using System.Collections.Generic;
    using LoRaWan;

    public class RegionLimits
    {
        /// <summary>
        /// Gets or sets The maximum and minimum datarate of a given region.
        /// </summary>
        public (Hertz Min, Hertz Max) FrequencyRange { get; set; }

        private readonly ISet<DataRate> downstreamValidDR;

        private readonly ISet<DataRate> upstreamValidDR;

        private readonly DataRateIndex startUpstreamDRIndex;

        private readonly DataRateIndex startDownstreamDRIndex;

        public RegionLimits((Hertz Min, Hertz Max) frequencyRange, ISet<DataRate> upstreamValidDR, ISet<DataRate> downstreamValidDR,
                            DataRateIndex startUpstreamDRIndex, DataRateIndex startDownstreamDRIndex)
        {
            FrequencyRange = frequencyRange;
            this.downstreamValidDR = downstreamValidDR;
            this.upstreamValidDR = upstreamValidDR;
            this.startDownstreamDRIndex = startDownstreamDRIndex;
            this.startUpstreamDRIndex = startUpstreamDRIndex;
        }

        public bool IsCurrentUpstreamDRValueWithinAcceptableValue(string datr) =>
            TryParseXpkDatr(datr) is { } dataRate && this.upstreamValidDR.Contains(dataRate);

        public bool IsCurrentDownstreamDRValueWithinAcceptableValue(string datr) =>
            TryParseXpkDatr(datr) is { } dataRate && this.downstreamValidDR.Contains(dataRate);

        private static DataRate? TryParseXpkDatr(string datr)
            => LoRaDataRate.TryParse(datr, out var loRaDataRate) ? loRaDataRate
             : FskDataRate.Fsk50000.XpkDatr == datr ? FskDataRate.Fsk50000
             : null;

        public bool IsCurrentUpstreamDRIndexWithinAcceptableValue(DataRateIndex dr) => (dr >= this.startUpstreamDRIndex) && dr < this.startUpstreamDRIndex + this.upstreamValidDR.Count;

        public bool IsCurrentDownstreamDRIndexWithinAcceptableValue(DataRateIndex? dr) => (dr >= this.startDownstreamDRIndex) && dr < this.startDownstreamDRIndex + this.downstreamValidDR.Count;
    }
}
