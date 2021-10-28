// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;

    public class RegionCN470 : Region
    {
        private const double FrequencyIncrement = 0.2;

        private readonly List<double> JoinFrequencies;

        private readonly List<double> RX2OTAADefaultFrequencies;

        private readonly List<List<double>> DownstreamFrequenciesByPlanType;

        public RegionCN470()
            : base(LoRaRegionType.CN470)
        {
            // Values assuming FOpts param is not used
            DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 59));
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

            var validDatarates = new HashSet<string>()
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

            MaxADRDataRate = 7;
            RegionLimits = new RegionLimits((min: 470.3, max: 509.7), validDatarates, validDatarates, 0, 0);

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

            this.RX2OTAADefaultFrequencies = new List<double>
            {
                485.3, 486.9, 488.5, 490.1, 491.7, 493.3, 494.9, 496.5, // 20 MHz plan A devices
                478.3, 498.3                                            // 20 MHz plan B devices
            };
        }

        /// <summary>
        /// Returns join channel indexfor region CN470 matching the frequency of the join request.
        /// </summary>
        /// <param name="joinChannel">Channel on which the join request was received.</param>
        public override bool TryGetJoinChannelIndex(Rxpk joinChannel, out int channelIndex)
        {
            if (joinChannel is null) throw new ArgumentNullException(nameof(joinChannel));

            channelIndex = this.JoinFrequencies.IndexOf(joinChannel.Freq);
            return channelIndex != -1;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="joinChannelIndex">index of the join channel.</param>
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, int? joinChannelIndex)
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

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo)
        {
            if (deviceJoinInfo is null) throw new ArgumentNullException(nameof(deviceJoinInfo));

            var rx2Window = new RX2ReceiveWindow { Frequency = 0, DataRate = 1 };

            // OTAA device
            if (deviceJoinInfo.ReportedCN470JoinChannel != null)
            {
                // 20 MHz plan A or B
                if (deviceJoinInfo.ReportedCN470JoinChannel < this.RX2OTAADefaultFrequencies.Count)
                {
                    rx2Window.Frequency = this.RX2OTAADefaultFrequencies[(int)deviceJoinInfo.ReportedCN470JoinChannel];
                }
                // 26 MHz plan A
                if (deviceJoinInfo.ReportedCN470JoinChannel <= 14)
                {
                    rx2Window.Frequency = 492.5;
                }
                // 26 MHz plan B
                if (deviceJoinInfo.ReportedCN470JoinChannel <= 19)
                {
                    rx2Window.Frequency = 502.5;
                }
            }

            // ABP device
            if (deviceJoinInfo.DesiredCN470JoinChannel != null)
            {
                // 20 MHz plan A
                if (deviceJoinInfo.DesiredCN470JoinChannel <= 7)
                {
                    rx2Window.Frequency = 486.9;
                }
                // 20 MHz plan B
                if (deviceJoinInfo.DesiredCN470JoinChannel <= 9)
                {
                    rx2Window.Frequency = 498.3;
                }
                // 26 MHz plan A
                if (deviceJoinInfo.DesiredCN470JoinChannel <= 14)
                {
                    rx2Window.Frequency = 492.5;
                }
                // 26 MHz plan B
                if (deviceJoinInfo.DesiredCN470JoinChannel <= 19)
                {
                    rx2Window.Frequency = 502.5;
                }
            }

            return rx2Window;
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
