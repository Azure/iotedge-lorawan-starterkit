// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using LoRaTools.LoRaPhysical;

    public class RegionUS915 : Region
    {
        public RegionUS915()
            : base(
                  LoRaRegionType.US915, 
                  0x34, 
                  null, // no GFSK in US Band
                  (frequency: 923.3, datr: 8),
                  1,
                  2,
                  5,
                  6,
                  16384,
                  64,
                  32,
                  (min: 1, max: 3))
        {
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region US915.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency)
        {
            frequency = 0;

            if (this.IsValidUpstreamRxpk(upstreamChannel))
            {
                int upstreamChannelNumber;
                // if DR4 the coding are different.
                if (upstreamChannel.Datr == "SF8BW500")
                {
                    // ==DR4
                    upstreamChannelNumber = 64 + (int)((upstreamChannel.Freq - 903) / 1.6);
                }
                else
                {
                    // if not DR4 other encoding
                    upstreamChannelNumber = (int)((upstreamChannel.Freq - 902.3) / 0.2);
                }

                frequency = Math.Round(923.3 + upstreamChannelNumber % 8 * 0.6, 1); // change to use a table
                return true;
            }

            return false;
        }
    }
}
