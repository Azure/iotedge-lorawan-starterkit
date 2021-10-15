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

        private readonly Dictionary<RegionCN470PlanType, List<double>> DownstreamFrequenciesPerPlan;

        public RegionCN470()
            : base(
                  LoRaRegionType.CN470,
                  //TODO: change below params
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
            DownstreamFrequenciesPerPlan = new Dictionary<RegionCN470PlanType, List<double>>
            {
                [RegionCN470PlanType.PlanA20MHz] = BuildFrequencyPlanList(483.9, 0, 31).Concat(BuildFrequencyPlanList(490.3, 32, 63)).ToList(),
                [RegionCN470PlanType.PlanB20MHz] = BuildFrequencyPlanList(476.9, 0, 32).Concat(BuildFrequencyPlanList(496.9, 32, 63)).ToList(),
                [RegionCN470PlanType.PlanA26MHz] = BuildFrequencyPlanList(490.1, 0, 23),
                [RegionCN470PlanType.PlanB26MHz] = BuildFrequencyPlanList(500.1, 0, 23)
            };
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

            //TODO: check IsValidUpstreamRxpk

            int channelNumber;

            switch (channelPlanType)
            {
                case RegionCN470PlanType.PlanA20MHz:
                    if (upstreamChannel.Freq < 500)
                        channelNumber = GetChannelNumber(upstreamChannel, 470.3);
                    else
                        channelNumber = GetChannelNumber(upstreamChannel, 503.5, 32);

                    frequency = DownstreamFrequenciesPerPlan[channelPlanType][channelNumber];
                    return true;

                case RegionCN470PlanType.PlanB20MHz:
                    if (upstreamChannel.Freq < 490)
                        channelNumber = GetChannelNumber(upstreamChannel, 476.9);
                    else
                        channelNumber = GetChannelNumber(upstreamChannel, 496.9, 32);

                    frequency = DownstreamFrequenciesPerPlan[channelPlanType][channelNumber];
                    return true;

                case RegionCN470PlanType.PlanA26MHz:
                    channelNumber = GetChannelNumber(upstreamChannel, 470.3);
                    frequency = DownstreamFrequenciesPerPlan[channelPlanType][channelNumber % 24];
                    return true;

                case RegionCN470PlanType.PlanB26MHz:
                    channelNumber = GetChannelNumber(upstreamChannel, 480.3);
                    frequency = DownstreamFrequenciesPerPlan[channelPlanType][channelNumber % 24];
                    return true;

                default:
                    return false;
            }
        }

        private static int GetChannelNumber(Rxpk upstreamChannel, double startUpstreamFreq, int startChannelNumber = 0) =>
            startChannelNumber + (int)Math.Round((upstreamChannel.Freq - startUpstreamFreq) / FrequencyIncrement, 0, MidpointRounding.AwayFromZero);
    }
}
