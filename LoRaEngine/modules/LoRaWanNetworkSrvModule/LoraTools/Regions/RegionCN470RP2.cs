// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;

    // Frequency plan for region CN470-510 using version RP002-1.0.3 of LoRaWAN Regional Parameters specification
    public class RegionCN470RP2 : Region
    {
        private const double FrequencyIncrement = 0.2;

        private readonly List<double> rx2OTAADefaultFrequencies;

        private readonly List<List<double>> downstreamFrequenciesByPlanType;

        // Dictionary mapping upstream join frequencies to a tuple containing
        // the corresponding downstream join frequency and the channel index
        public Dictionary<double, (double downstreamFreq, int joinChannelIndex)> UpstreamJoinFrequenciesToDownstreamAndChannelIndex { get; }

        public RegionCN470RP2()
            : base(LoRaRegionType.CN470RP2)
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

            UpstreamJoinFrequenciesToDownstreamAndChannelIndex = new Dictionary<double, (double, int)>
            {
                { 470.9, (484.5, 0) }, { 472.5, (486.1, 1) }, { 474.1, (487.7, 2) }, { 475.7, (489.3, 3) }, { 504.1, (490.9, 4) },
                { 505.7, (492.5, 5) }, { 507.3, (494.1, 6) }, { 508.9, (495.7, 7) }, { 479.9, (479.9, 8) }, { 499.9, (499.9, 9) },
                { 470.3, (492.5, 10) }, { 472.3, (492.5, 11) }, { 474.3, (492.5, 12) }, { 476.3, (492.5, 13) }, { 478.3, (492.5, 14) },
                { 480.3, (502.5, 15) }, { 482.3, (502.5, 16) }, { 484.3, (502.5, 17) }, { 486.3, (502.5, 18) }, { 488.3, (502.5, 19) }
            };

            this.downstreamFrequenciesByPlanType = new List<List<double>>
            {
                BuildFrequencyPlanList(483.9, 0, 31).Concat(BuildFrequencyPlanList(490.3, 32, 63)).ToList(),
                BuildFrequencyPlanList(476.9, 0, 31).Concat(BuildFrequencyPlanList(496.9, 32, 63)).ToList(),
                BuildFrequencyPlanList(490.1, 0, 23),
                BuildFrequencyPlanList(500.1, 0, 23)
            };

            this.rx2OTAADefaultFrequencies = new List<double>
            {
                485.3, 486.9, 488.5, 490.1, 491.7, 493.3, 494.9, 496.5, // 20 MHz plan A devices
                478.3, 498.3                                            // 20 MHz plan B devices
            };
        }

        /// <summary>
        /// Returns join channel index for region CN470 matching the frequency of the join request.
        /// </summary>
        /// <param name="joinChannel">Channel on which the join request was received.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public override bool TryGetJoinChannelIndex(Rxpk joinChannel, out int channelIndex)
        {
            if (joinChannel is null) throw new ArgumentNullException(nameof(joinChannel));

            channelIndex = -1;

            if (UpstreamJoinFrequenciesToDownstreamAndChannelIndex.TryGetValue(joinChannel.Freq, out var elem))
            {
                channelIndex = elem.joinChannelIndex;
            }

            return channelIndex != -1;
        }

        /// <summary>
        /// Returns join channel index for region CN470 matching the frequency of the join request.
        /// </summary>
        public override bool TryGetJoinChannelIndex(double frequency, out int channelIndex)
        {
            channelIndex = -1;

            if (UpstreamJoinFrequenciesToDownstreamAndChannelIndex.TryGetValue(frequency, out var elem))
            {
                channelIndex = elem.joinChannelIndex;
            }

            return channelIndex != -1;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, DeviceJoinInfo deviceJoinInfo)
        {
            if (deviceJoinInfo is null) throw new ArgumentNullException(nameof(deviceJoinInfo));

            if (!IsValidUpstreamRxpk(upstreamChannel))
                throw new LoRaProcessingException($"Invalid upstream channel: {upstreamChannel.Freq}, {upstreamChannel.Datr}.");

            frequency = 0;

            // We prioritize the selection of join channel index from reported twin properties (set for OTAA devices)
            // over desired twin properties (set for APB devices).
            var joinChannelIndex = deviceJoinInfo.ReportedCN470JoinChannel ?? deviceJoinInfo.DesiredCN470JoinChannel;

            if (joinChannelIndex == null)
                return false;

            int channelNumber;

            // 20 MHz plan A
            if (joinChannelIndex <= 7)
            {
                channelNumber = upstreamChannel.Freq < 500 ? GetChannelNumber(upstreamChannel, 470.3) : GetChannelNumber(upstreamChannel, 503.5, 32);
                frequency = this.downstreamFrequenciesByPlanType[0][channelNumber];
                return true;
            }
            // 20 MHz plan B
            if (joinChannelIndex <= 9)
            {
                channelNumber = upstreamChannel.Freq < 490 ? GetChannelNumber(upstreamChannel, 476.9) : GetChannelNumber(upstreamChannel, 496.9, 32);
                frequency = this.downstreamFrequenciesByPlanType[1][channelNumber];
                return true;
            }
            // 26 MHz plan A
            if (joinChannelIndex <= 14)
            {
                channelNumber = GetChannelNumber(upstreamChannel, 470.3);
                frequency = this.downstreamFrequenciesByPlanType[2][channelNumber % 24];
                return true;
            }
            // 26 MHz plan B
            if (joinChannelIndex <= 19)
            {
                channelNumber = GetChannelNumber(upstreamChannel, 480.3);
                frequency = this.downstreamFrequenciesByPlanType[3][channelNumber % 24];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// <param name="upstreamFrequency">The frequency at which the message was transmitted.</param>
        /// <param name="upstreamDataRate">The upstream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        /// </summary>
        public override bool TryGetDownstreamChannelFrequency(double upstreamFrequency, out double downstreamFrequency, ushort? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = default)
        {
            if (deviceJoinInfo is null) throw new ArgumentNullException(nameof(deviceJoinInfo));

            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            downstreamFrequency = 0;

            // We prioritize the selection of join channel index from reported twin properties (set for OTAA devices)
            // over desired twin properties (set for APB devices).
            var joinChannelIndex = deviceJoinInfo.ReportedCN470JoinChannel ?? deviceJoinInfo.DesiredCN470JoinChannel;

            if (joinChannelIndex == null)
                return false;

            int channelNumber;

            // 20 MHz plan A
            if (joinChannelIndex <= 7)
            {
                channelNumber = upstreamFrequency < 500 ? GetChannelNumber(upstreamFrequency, 470.3) : GetChannelNumber(upstreamFrequency, 503.5, 32);
                downstreamFrequency = this.downstreamFrequenciesByPlanType[0][channelNumber];
                return true;
            }
            // 20 MHz plan B
            if (joinChannelIndex <= 9)
            {
                channelNumber = upstreamFrequency < 490 ? GetChannelNumber(upstreamFrequency, 476.9) : GetChannelNumber(upstreamFrequency, 496.9, 32);
                downstreamFrequency = this.downstreamFrequenciesByPlanType[1][channelNumber];
                return true;
            }
            // 26 MHz plan A
            if (joinChannelIndex <= 14)
            {
                channelNumber = GetChannelNumber(upstreamFrequency, 470.3);
                downstreamFrequency = this.downstreamFrequenciesByPlanType[2][channelNumber % 24];
                return true;
            }
            // 26 MHz plan B
            if (joinChannelIndex <= 19)
            {
                channelNumber = GetChannelNumber(upstreamFrequency, 480.3);
                downstreamFrequency = this.downstreamFrequenciesByPlanType[3][channelNumber % 24];
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

            // Default data rate is always 1 for CN470
            ushort dataRate = 1;

            var rx2Window = new RX2ReceiveWindow(default, dataRate);

            // OTAA device
            if (deviceJoinInfo.ReportedCN470JoinChannel != null)
            {
                // 20 MHz plan A or B
                if (deviceJoinInfo.ReportedCN470JoinChannel < this.rx2OTAADefaultFrequencies.Count)
                {
                    return new RX2ReceiveWindow(this.rx2OTAADefaultFrequencies[(int)deviceJoinInfo.ReportedCN470JoinChannel], dataRate);
                }
                // 26 MHz plan A
                else if (deviceJoinInfo.ReportedCN470JoinChannel <= 14)
                {
                    return new RX2ReceiveWindow(492.5, dataRate);
                }
                // 26 MHz plan B
                else if (deviceJoinInfo.ReportedCN470JoinChannel <= 19)
                {
                    return new RX2ReceiveWindow(502.5, dataRate);
                }
            }

            // ABP device
            else if (deviceJoinInfo.DesiredCN470JoinChannel != null)
            {
                // 20 MHz plan A
                if (deviceJoinInfo.DesiredCN470JoinChannel <= 7)
                {
                    return new RX2ReceiveWindow(486.9, dataRate);
                }
                // 20 MHz plan B
                else if (deviceJoinInfo.DesiredCN470JoinChannel <= 9)
                {
                    return new RX2ReceiveWindow(498.3, dataRate);
                }
                // 26 MHz plan A
                else if (deviceJoinInfo.DesiredCN470JoinChannel <= 14)
                {
                    return new RX2ReceiveWindow(492.5, dataRate);
                }
                // 26 MHz plan B
                else if (deviceJoinInfo.DesiredCN470JoinChannel <= 19)
                {
                    return new RX2ReceiveWindow(502.5, dataRate);
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

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        private static int GetChannelNumber(Rxpk upstreamChannel, double startUpstreamFreq, int startChannelNumber = 0) =>
            startChannelNumber + (int)Math.Round((upstreamChannel.Freq - startUpstreamFreq) / FrequencyIncrement, 0, MidpointRounding.AwayFromZero);
        private static int GetChannelNumber(double upstreamChannelFrequency, double startUpstreamFreq, int startChannelNumber = 0) =>
            startChannelNumber + (int)Math.Round((upstreamChannelFrequency - startUpstreamFreq) / FrequencyIncrement, 0, MidpointRounding.AwayFromZero);
    }
}
