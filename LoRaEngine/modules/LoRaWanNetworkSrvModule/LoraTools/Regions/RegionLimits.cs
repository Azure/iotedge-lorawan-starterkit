// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System.Collections.Generic;
    using Org.BouncyCastle.Utilities.Collections;

    public class RegionLimits
    {
        /// <summary>
        /// Gets or sets The maximum and minimum datarate of a given region
        /// </summary>
        public (double min, double max) FrequencyRange { get; set; }

        private HashSet<string> downstreamValidDR;

        private HashSet<string> upstreamValidDR;

        private uint startUpstreamDRIndex;

        private uint startDownstreamDRIndex;

        public RegionLimits((double min, double max) frequencyRange, HashSet<string> upstreamValidDR, HashSet<string> downstreamValidDR, uint startUpstreamDRIndex, uint startDownstreamDRIndex)
        {
            this.FrequencyRange = frequencyRange;
            this.downstreamValidDR = downstreamValidDR;
            this.upstreamValidDR = upstreamValidDR;
            this.startDownstreamDRIndex = startDownstreamDRIndex;
            this.startUpstreamDRIndex = startUpstreamDRIndex;
        }

        public bool IsCurrentUpstreamDRValueWithinAcceptableValue(string dr) => this.upstreamValidDR.Contains(dr);

        public bool IsCurrentDownstreamDRValueWithinAcceptableValue(string dr) => this.downstreamValidDR.Contains(dr);

        public bool IsCurrentUpstreamDRIndexWithinAcceptableValue(uint dr) => (dr >= this.startUpstreamDRIndex) && dr < this.startUpstreamDRIndex + this.upstreamValidDR.Count;

        public bool IsCurrentDownstreamDRIndexWithinAcceptableValue(uint dr) => (dr >= this.startDownstreamDRIndex) && dr < this.startDownstreamDRIndex + this.downstreamValidDR.Count;
    }
}
