// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        private readonly HashSet<string> downstreamValidDR;

        private readonly HashSet<string> upstreamValidDR;

        private readonly DataRate startUpstreamDRIndex;

        private readonly DataRate startDownstreamDRIndex;

        public RegionLimits((Hertz Min, Hertz Max) frequencyRange, HashSet<string> upstreamValidDR, HashSet<string> downstreamValidDR,
                            DataRate startUpstreamDRIndex, DataRate startDownstreamDRIndex)
        {
            FrequencyRange = frequencyRange;
            this.downstreamValidDR = downstreamValidDR;
            this.upstreamValidDR = upstreamValidDR;
            this.startDownstreamDRIndex = startDownstreamDRIndex;
            this.startUpstreamDRIndex = startUpstreamDRIndex;
        }

        public bool IsCurrentUpstreamDRValueWithinAcceptableValue(string dr) => this.upstreamValidDR.Contains(dr);

        public bool IsCurrentDownstreamDRValueWithinAcceptableValue(string dr) => this.downstreamValidDR.Contains(dr);

        public bool IsCurrentUpstreamDRIndexWithinAcceptableValue(DataRate dr) => (dr >= this.startUpstreamDRIndex) && dr < this.startUpstreamDRIndex + this.upstreamValidDR.Count;

        public bool IsCurrentDownstreamDRIndexWithinAcceptableValue(DataRate? dr) => (dr >= this.startDownstreamDRIndex) && dr < this.startDownstreamDRIndex + this.downstreamValidDR.Count;
    }
}
