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

        private readonly List<Tuple<HashSet<double>, RegionCN470PlanType>> JoinFrequenciesToPlanType;

        private readonly Dictionary<RegionCN470PlanType, List<double>> PlanTypeToDownstreamFrequencies;

        public RegionCN470()
            : base(
                  LoRaRegionType.CN470,
                  0x34,
                  null,
                  (frequency: 485.3, datr: 1)) // TODO: support multiple RX2 receive windows, see #561
        {
            PlanTypeToDownstreamFrequencies = new Dictionary<RegionCN470PlanType, List<double>>
            {
                [RegionCN470PlanType.PlanA20MHz] = BuildFrequencyPlanList(483.9, 0, 31).Concat(BuildFrequencyPlanList(490.3, 32, 63)).ToList(),
                [RegionCN470PlanType.PlanB20MHz] = BuildFrequencyPlanList(476.9, 0, 32).Concat(BuildFrequencyPlanList(496.9, 32, 63)).ToList(),
                [RegionCN470PlanType.PlanA26MHz] = BuildFrequencyPlanList(490.1, 0, 23),
                [RegionCN470PlanType.PlanB26MHz] = BuildFrequencyPlanList(500.1, 0, 23)
            };

            JoinFrequenciesToPlanType = new List<Tuple<HashSet<double>, RegionCN470PlanType>>
            {
                Tuple.Create(new HashSet<double> { 470.9, 472.5, 474.1, 475.7, 504.1, 505.7, 507.3, 508.9 }, RegionCN470PlanType.PlanA20MHz ),
                Tuple.Create(new HashSet<double> { 479.9, 499.9 }, RegionCN470PlanType.PlanB20MHz ),
                Tuple.Create(new HashSet<double> { 470.3, 472.3, 474.3, 476.3, 478.3 }, RegionCN470PlanType.PlanA26MHz ),
                Tuple.Create(new HashSet<double> { 480.3, 482.3, 484.3, 486.3, 488.3 }, RegionCN470PlanType.PlanB26MHz ),
            };
        }

        /// <summary>
        /// Returns channel plan type for region CN470 matching the frequency of the join request.
        /// </summary>
        /// <param name="joinChannel">Channel on which the join request was received.</param>
        public override bool TryGetChannelPlanType(Rxpk joinChannel, out string channelPlan)
        {
            if (joinChannel is null) throw new ArgumentNullException(nameof(joinChannel));

            foreach (var (joinFrequencies, planType) in JoinFrequenciesToPlanType)
            {
                if (joinFrequencies.Contains(joinChannel.Freq))
                {
                    channelPlan = planType.ToString();
                    return true;
                }
            }
            channelPlan = string.Empty;
            return false;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="channelPlan">optional active channel plan type to be used for the given region, if applicable.</param>
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, string channelPlan = null)
        {
            frequency = 0;

            if (channelPlan == null)
                return false;

            if (!Enum.TryParse<RegionCN470PlanType>(channelPlan, true, out var channelPlanType))
                return false;

            if (!IsValidUpstreamRxpk(upstreamChannel))
                return false;

            int channelNumber;

            switch (channelPlanType)
            {
                case RegionCN470PlanType.PlanA20MHz:
                    channelNumber = upstreamChannel.Freq < 500 ? GetChannelNumber(upstreamChannel, 470.3) : GetChannelNumber(upstreamChannel, 503.5, 32);
                    frequency = PlanTypeToDownstreamFrequencies[channelPlanType][channelNumber];
                    return true;

                case RegionCN470PlanType.PlanB20MHz:
                    channelNumber = upstreamChannel.Freq < 490 ? GetChannelNumber(upstreamChannel, 476.9) : GetChannelNumber(upstreamChannel, 496.9, 32);
                    frequency = PlanTypeToDownstreamFrequencies[channelPlanType][channelNumber];
                    return true;

                case RegionCN470PlanType.PlanA26MHz:
                    channelNumber = GetChannelNumber(upstreamChannel, 470.3);
                    frequency = PlanTypeToDownstreamFrequencies[channelPlanType][channelNumber % 24];
                    return true;

                case RegionCN470PlanType.PlanB26MHz:
                    channelNumber = GetChannelNumber(upstreamChannel, 480.3);
                    frequency = PlanTypeToDownstreamFrequencies[channelPlanType][channelNumber % 24];
                    return true;

                default:
                    return false;
            }
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
