// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.LoRaPhysical;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class RegionCN470 : Region
    {
        private const double FrequencyIncrement = 0.2;

        private readonly List<double> JoinFrequencies;

        private readonly List<List<double>> DownstreamFrequenciesByPlanType;

        public RegionCN470()
            : base(
                  LoRaRegionType.CN470,
                  0x34,
                  null,
                  (frequency: 485.3, datr: 1)) // TODO: support multiple RX2 receive windows, see #561
        {
            DownstreamFrequenciesByPlanType = new List<List<double>>
            {
                BuildFrequencyPlanList(483.9, 0, 31).Concat(BuildFrequencyPlanList(490.3, 32, 63)).ToList(),
                BuildFrequencyPlanList(476.9, 0, 32).Concat(BuildFrequencyPlanList(496.9, 32, 63)).ToList(),
                BuildFrequencyPlanList(490.1, 0, 23),
                BuildFrequencyPlanList(500.1, 0, 23)
            };

            JoinFrequencies = new List<double>
            {
                470.9, 472.5, 474.1, 475.7, 504.1, 505.7, 507.3, 508.9, 479.9, 499.9,
                470.3, 472.3, 474.3, 476.3, 478.3, 480.3, 482.3, 484.3, 486.3, 488.3
            };
        }

        /// <summary>
        /// Returns join channel indexfor region CN470 matching the frequency of the join request.
        /// </summary>
        /// <param name="joinChannel">Channel on which the join request was received.</param>
        public override bool TryGetJoinChannelIndex(Rxpk joinChannel, out int channelIndex)
        {
            if (joinChannel is null) throw new ArgumentNullException(nameof(joinChannel));

            for (var index = 0; index < JoinFrequencies.Count; ++index)
            {
                if (JoinFrequencies[index] == joinChannel.Freq)
                {
                    channelIndex = index;
                    return true;
                }
            }
            channelIndex = -1;
            return false;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="joinChannelIndex">index of the join channel.</param>
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, int? joinChannelIndex = null)
        {
            frequency = 0;

            if (joinChannelIndex == null)
                return false;

            if (!IsValidUpstreamRxpk(upstreamChannel))
                return false;

            int channelNumber;

            // 20 MHz plan A
            if (joinChannelIndex <= 7)
            {
                channelNumber = upstreamChannel.Freq < 500 ? GetChannelNumber(upstreamChannel, 470.3) : GetChannelNumber(upstreamChannel, 503.5, 32);
                frequency = DownstreamFrequenciesByPlanType[0][channelNumber];
                return true;
            }
            // 20 MHz plan B
            if (joinChannelIndex <= 9)
            {
                channelNumber = upstreamChannel.Freq < 490 ? GetChannelNumber(upstreamChannel, 476.9) : GetChannelNumber(upstreamChannel, 496.9, 32);
                frequency = DownstreamFrequenciesByPlanType[1][channelNumber];
                return true;
            }
            // 26 MHz plan A
            if (joinChannelIndex <= 14)
            {
                channelNumber = GetChannelNumber(upstreamChannel, 470.3);
                frequency = DownstreamFrequenciesByPlanType[2][channelNumber % 24];
                return true;
            }
            // 26 MHz plan B
            if (joinChannelIndex <= 19)
            {
                channelNumber = GetChannelNumber(upstreamChannel, 480.3);
                frequency = DownstreamFrequenciesByPlanType[3][channelNumber % 24];
                return true;
            }

            return false;
        }

        private static List<double> BuildFrequencyPlanList(double startFrequency, int startChannel, int endChannel)
        {
            var frequencies = new List<double>();
            var currentFreq = startFrequency;

            for (var channel = startChannel; channel <= endChannel; ++channel)
            {
                frequencies.Add(currentFreq);
                currentFreq += FrequencyIncrement;
            }

            return frequencies;
        }

        private static int GetChannelNumber(Rxpk upstreamChannel, double startUpstreamFreq, int startChannelNumber = 0) =>
            startChannelNumber + (int)Math.Round((upstreamChannel.Freq - startUpstreamFreq) / FrequencyIncrement, 0, MidpointRounding.AwayFromZero);
    }
}
