// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class Datarate
    {
        public uint SpreadingFactor { get; set; }

        public uint BandWidth { get; set; }

        public Datarate(uint spreadingFactor, uint bandwidth)
        {
            this.SpreadingFactor = spreadingFactor;
            this.BandWidth = bandwidth;
        }

        public override string ToString()
        {
            return $"SF{this.SpreadingFactor}BW{this.BandWidth}";
        }
    }
}
