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

        public HashSet<string> DownstreamValidDR { get; set; }

        public HashSet<string> UpstreamValidDR { get; set; }

        public RegionLimits((double min, double max) frequencyRange, HashSet<string> upstreamValidDR, HashSet<string> downstreamValidDR)
        {
            this.FrequencyRange = frequencyRange;
            this.DownstreamValidDR = downstreamValidDR;
            this.UpstreamValidDR = upstreamValidDR;
        }
    }
}
