// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.LoRaPhysical;

    public class RegionCN470 : Region
    {
        public RegionCN470()
            : base(
                  LoRaRegionType.CN470,
                  // TODO: change below params
                  0x34,
                  null,
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
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency)
        {
            frequency = 0;

            return false;
        }
    }
}
