// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;

    public class RegionLimits
    {
        /// <summary>
        /// Gets or sets The maximum and minimum datarate of a given region
        /// </summary>
        public (double min, double max) FrequencyRange { get; set; }

        public List<string> DatarateRange { get; set; }

        public bool IsCurrentDRIndexWithinAcceptableValue(uint dr)
        {
            if (dr < this.DatarateRange.Count)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public RegionLimits((double min, double max) frequencyRange, List<string> datarateRange)
        {
            this.FrequencyRange = frequencyRange;
            this.DatarateRange = datarateRange;
        }
    }
}
