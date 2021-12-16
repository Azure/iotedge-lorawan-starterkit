// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaWan;

    public class RegionLimits
    {
        /// <summary>
        /// Gets or sets The maximum and minimum datarate of a given region.
        /// </summary>
        public (Hertz Min, Hertz Max) FrequencyRange { get; set; }

        private readonly HashSet<DataRate> downstreamValidDR;

        private readonly HashSet<DataRate> upstreamValidDR;

        private readonly DataRateIndex startUpstreamDRIndex;

        private readonly DataRateIndex startDownstreamDRIndex;

        public RegionLimits((Hertz Min, Hertz Max) frequencyRange, HashSet<DataRate> upstreamValidDR, HashSet<DataRate> downstreamValidDR,
                            DataRateIndex startUpstreamDRIndex, DataRateIndex startDownstreamDRIndex)
        {
            FrequencyRange = frequencyRange;
            this.downstreamValidDR = downstreamValidDR;
            this.upstreamValidDR = upstreamValidDR;
            this.startDownstreamDRIndex = startDownstreamDRIndex;
            this.startUpstreamDRIndex = startUpstreamDRIndex;
        }

        public bool IsCurrentUpstreamDRValueWithinAcceptableValue(string dr) => this.upstreamValidDR.Contains(ParseXpkDatr(dr));

        public bool IsCurrentDownstreamDRValueWithinAcceptableValue(string dr) => this.downstreamValidDR.Contains(ParseXpkDatr(dr));

        private static DataRate ParseXpkDatr(string datr)
            => LoRaDataRate.TryParse(datr, out var loRaDataRate) ? loRaDataRate
             : FskDataRate.Fsk50000.XpkDatr == datr ? FskDataRate.Fsk50000
             : throw new FormatException(@"Invalid ""datr"": " + datr);

        public bool IsCurrentUpstreamDRIndexWithinAcceptableValue(DataRateIndex dr) => (dr >= this.startUpstreamDRIndex) && dr < this.startUpstreamDRIndex + this.upstreamValidDR.Count;

        public bool IsCurrentDownstreamDRIndexWithinAcceptableValue(DataRateIndex? dr) => (dr >= this.startDownstreamDRIndex) && dr < this.startDownstreamDRIndex + this.downstreamValidDR.Count;
    }
}
