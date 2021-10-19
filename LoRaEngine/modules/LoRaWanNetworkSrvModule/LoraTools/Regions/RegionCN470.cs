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
            // Values assuming FOpts param is not used
            DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 31)); // documentation has no max payload size for DR 0
            DRtoConfiguration.Add(1, (configuration: "SF11BW125", maxPyldSize: 31));
            DRtoConfiguration.Add(2, (configuration: "SF10BW125", maxPyldSize: 94));
            DRtoConfiguration.Add(3, (configuration: "SF9BW125", maxPyldSize: 192));
            DRtoConfiguration.Add(4, (configuration: "SF8BW125", maxPyldSize: 250));
            DRtoConfiguration.Add(5, (configuration: "SF7BW125", maxPyldSize: 250));
            DRtoConfiguration.Add(6, (configuration: "SF7BW500", maxPyldSize: 250));
            DRtoConfiguration.Add(7, (configuration: "50", maxPyldSize: 250)); // FSK 50

            TXPowertoMaxEIRP.Add(0, 19);
            TXPowertoMaxEIRP.Add(1, 17);
            TXPowertoMaxEIRP.Add(2, 15);
            TXPowertoMaxEIRP.Add(3, 13);
            TXPowertoMaxEIRP.Add(4, 11);
            TXPowertoMaxEIRP.Add(5, 9);
            TXPowertoMaxEIRP.Add(6, 7);
            TXPowertoMaxEIRP.Add(7, 5);

            RX1DROffsetTable = new int[8][]
            {
                new int[] { 0, 0, 0, 0, 0, 0 },
                new int[] { 1, 1, 1, 1, 1, 1 },
                new int[] { 2, 1, 1, 1, 1, 1 },
                new int[] { 3, 2, 1, 1, 1, 1 },
                new int[] { 4, 3, 2, 1, 1, 1 },
                new int[] { 5, 4, 3, 2, 1, 1 },
                new int[] { 6, 5, 4, 3, 2, 1 },
                new int[] { 7, 6, 5, 4, 3, 2 },
            };

            var validDataranges = new HashSet<string>()
            {
                "SF12BW125", // 0
                "SF11BW125", // 1
                "SF10BW125", // 2 
                "SF9BW125",  // 3
                "SF8BW125",  // 4
                "SF7BW125",  // 5
                "SF7BW500",  // 6
                "50"         // 7 FSK 50
            };

            MaxADRDataRate = 7; // needs to be clarified
            RegionLimits = new RegionLimits((min: 470.3, max: 509.7), validDataranges, validDataranges, 0, 0);

            this.DownstreamFrequenciesByPlanType = new List<List<double>>
            {
                BuildFrequencyPlanList(483.9, 0, 31).Concat(BuildFrequencyPlanList(490.3, 32, 63)).ToList(),
                BuildFrequencyPlanList(476.9, 0, 31).Concat(BuildFrequencyPlanList(496.9, 32, 63)).ToList(),
                BuildFrequencyPlanList(490.1, 0, 23),
                BuildFrequencyPlanList(500.1, 0, 23)
            };

            this.JoinFrequencies = new List<double>
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

            for (var index = 0; index < this.JoinFrequencies.Count; ++index)
            {
                if (this.JoinFrequencies[index] == joinChannel.Freq)
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
                frequency = this.DownstreamFrequenciesByPlanType[0][channelNumber];
                return true;
            }
            // 20 MHz plan B
            if (joinChannelIndex <= 9)
            {
                channelNumber = upstreamChannel.Freq < 490 ? GetChannelNumber(upstreamChannel, 476.9) : GetChannelNumber(upstreamChannel, 496.9, 32);
                frequency = this.DownstreamFrequenciesByPlanType[1][channelNumber];
                return true;
            }
            // 26 MHz plan A
            if (joinChannelIndex <= 14)
            {
                channelNumber = GetChannelNumber(upstreamChannel, 470.3);
                frequency = this.DownstreamFrequenciesByPlanType[2][channelNumber % 24];
                return true;
            }
            // 26 MHz plan B
            if (joinChannelIndex <= 19)
            {
                channelNumber = GetChannelNumber(upstreamChannel, 480.3);
                frequency = this.DownstreamFrequenciesByPlanType[3][channelNumber % 24];
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
                frequencies.Add(Math.Round(currentFreq, 1, MidpointRounding.AwayFromZero));
                currentFreq += FrequencyIncrement;
            }

            return frequencies;
        }

        private static int GetChannelNumber(Rxpk upstreamChannel, double startUpstreamFreq, int startChannelNumber = 0) =>
            startChannelNumber + (int)Math.Round((upstreamChannel.Freq - startUpstreamFreq) / FrequencyIncrement, 0, MidpointRounding.AwayFromZero);
    }
}
