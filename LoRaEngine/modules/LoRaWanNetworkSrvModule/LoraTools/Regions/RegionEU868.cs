// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;

    public class RegionEU868 : Region
    {
        public RegionEU868()
            : base(
                  LoRaRegionType.EU868,
                  0x34,
                  ConversionHelper.StringToByteArray("C194C1"),
                  (frequency: 869.525, datr: 0),
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
        /// Logic to get the correct downstream transmission frequency for region EU868.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency)
        {
            frequency = 0;

            if (IsValidUpstreamRxpk(upstreamChannel))
            {
                // in case of EU, you respond on same frequency as you sent data.
                frequency = upstreamChannel.Freq;
                return true;
            }

            return false;
        }
    }
}
