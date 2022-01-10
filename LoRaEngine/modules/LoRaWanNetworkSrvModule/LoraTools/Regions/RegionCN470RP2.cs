// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using LoRaTools.Utils;
    using LoRaWan;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    // Frequency plan for region CN470-510 using version RP002-1.0.3 of LoRaWAN Regional Parameters specification
    public class RegionCN470RP2 : Region
    {
        private static readonly Mega FrequencyIncrement = new(0.2);

        private readonly List<Hertz> rx2OTAADefaultFrequencies;

        private readonly List<List<Hertz>> downstreamFrequenciesByPlanType;

        // Dictionary mapping upstream join frequencies to a tuple containing
        // the corresponding downstream join frequency and the channel index
        public Dictionary<Hertz, (Hertz downstreamFreq, int joinChannelIndex)> UpstreamJoinFrequenciesToDownstreamAndChannelIndex { get; }

        private static readonly ImmutableDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DrToConfigurationByDrIndex =
            new Dictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)>
            {
                // Values assuming FOpts param is not used
                [DR0] = (LoRaDataRate.SF12BW125, MaxPayloadSize: 59),
                [DR1] = (LoRaDataRate.SF11BW125, MaxPayloadSize: 31),
                [DR2] = (LoRaDataRate.SF10BW125, MaxPayloadSize: 94),
                [DR3] = (LoRaDataRate.SF9BW125, MaxPayloadSize: 192),
                [DR4] = (LoRaDataRate.SF8BW125, MaxPayloadSize: 250),
                [DR5] = (LoRaDataRate.SF7BW125, MaxPayloadSize: 250),
                [DR6] = (LoRaDataRate.SF7BW500, MaxPayloadSize: 250),
                [DR7] = (FskDataRate.Fsk50000, MaxPayloadSize: 250),
            }.ToImmutableDictionary();

        public override IReadOnlyDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DRtoConfiguration => DrToConfigurationByDrIndex;

        private static readonly ImmutableDictionary<uint, double> MaxEirpByTxPower =
            new Dictionary<uint, double>
            {
                [0] = 19,
                [1] = 17,
                [2] = 15,
                [3] = 13,
                [4] = 11,
                [5] = 9,
                [6] = 7,
                [7] = 5,
            }.ToImmutableDictionary();

        public override IReadOnlyDictionary<uint, double> TXPowertoMaxEIRP => MaxEirpByTxPower;

        private static readonly ImmutableArray<IReadOnlyList<DataRateIndex>> RX1DROffsetTableInternal =
            new IReadOnlyList<DataRateIndex>[]
            {
                new[] { DR0, DR0, DR0, DR0, DR0, DR0 }.ToImmutableArray(),
                new[] { DR1, DR1, DR1, DR1, DR1, DR1 }.ToImmutableArray(),
                new[] { DR2, DR1, DR1, DR1, DR1, DR1 }.ToImmutableArray(),
                new[] { DR3, DR2, DR1, DR1, DR1, DR1 }.ToImmutableArray(),
                new[] { DR4, DR3, DR2, DR1, DR1, DR1 }.ToImmutableArray(),
                new[] { DR5, DR4, DR3, DR2, DR1, DR1 }.ToImmutableArray(),
                new[] { DR6, DR5, DR4, DR3, DR2, DR1 }.ToImmutableArray(),
                new[] { DR7, DR6, DR5, DR4, DR3, DR2 }.ToImmutableArray(),
            }.ToImmutableArray();

        public override IReadOnlyList<IReadOnlyList<DataRateIndex>> RX1DROffsetTable => RX1DROffsetTableInternal;

        public RegionCN470RP2()
            : base(LoRaRegionType.CN470RP2)
        {
            var validDatarates = new HashSet<DataRate>
            {
                LoRaDataRate.SF12BW125, // 0
                LoRaDataRate.SF11BW125, // 1
                LoRaDataRate.SF10BW125, // 2
                LoRaDataRate.SF9BW125,  // 3
                LoRaDataRate.SF8BW125,  // 4
                LoRaDataRate.SF7BW125,  // 5
                LoRaDataRate.SF7BW500,  // 6
                FskDataRate.Fsk50000    // 7
            };

            MaxADRDataRate = DR7;
            RegionLimits = new RegionLimits((Min: Mega(470.3), Max: Mega(509.7)), validDatarates, validDatarates, 0, 0);

            UpstreamJoinFrequenciesToDownstreamAndChannelIndex = new Dictionary<Hertz, (Hertz, int)>
            {
                [Mega(470.9)] = (Mega(484.5), 0),
                [Mega(472.5)] = (Mega(486.1), 1),
                [Mega(474.1)] = (Mega(487.7), 2),
                [Mega(475.7)] = (Mega(489.3), 3),
                [Mega(504.1)] = (Mega(490.9), 4),
                [Mega(505.7)] = (Mega(492.5), 5),
                [Mega(507.3)] = (Mega(494.1), 6),
                [Mega(508.9)] = (Mega(495.7), 7),
                [Mega(479.9)] = (Mega(479.9), 8),
                [Mega(499.9)] = (Mega(499.9), 9),
                [Mega(470.3)] = (Mega(492.5), 10),
                [Mega(472.3)] = (Mega(492.5), 11),
                [Mega(474.3)] = (Mega(492.5), 12),
                [Mega(476.3)] = (Mega(492.5), 13),
                [Mega(478.3)] = (Mega(492.5), 14),
                [Mega(480.3)] = (Mega(502.5), 15),
                [Mega(482.3)] = (Mega(502.5), 16),
                [Mega(484.3)] = (Mega(502.5), 17),
                [Mega(486.3)] = (Mega(502.5), 18),
                [Mega(488.3)] = (Mega(502.5), 19)
            };

            this.downstreamFrequenciesByPlanType = new List<List<Hertz>>
            {
                ListFrequencyPlan(Mega(483.9), 0, 31).Concat(ListFrequencyPlan(Mega(490.3), 32, 63)).ToList(),
                ListFrequencyPlan(Mega(476.9), 0, 31).Concat(ListFrequencyPlan(Mega(496.9), 32, 63)).ToList(),
                ListFrequencyPlan(Mega(490.1), 0, 23).ToList(),
                ListFrequencyPlan(Mega(500.1), 0, 23).ToList()
            };

            static IEnumerable<Hertz> ListFrequencyPlan(Hertz startFrequency, int startChannel, int endChannel)
            {
                var currentFreq = startFrequency;

                for (var channel = startChannel; channel <= endChannel; ++channel)
                {
                    yield return currentFreq;
                    currentFreq += FrequencyIncrement;
                }
            }

            this.rx2OTAADefaultFrequencies = new List<Hertz>
            {
                // 20 MHz plan A devices
                Mega(485.3), Mega(486.9), Mega(488.5), Mega(490.1),
                Mega(491.7), Mega(493.3), Mega(494.9), Mega(496.5),
                // 20 MHz plan B devices
                Mega(478.3), Mega(498.3),
            };
        }

        /// <summary>
        /// Returns join channel index for region CN470 matching the frequency of the join request.
        /// </summary>
        public override bool TryGetJoinChannelIndex(Hertz frequency, out int channelIndex)
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
        /// <param name="upstreamFrequency">The frequency at which the message was transmitted.</param>
        /// <param name="upstreamDataRate">The upstream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        /// </summary>
        public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, DataRateIndex? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = default)
        {
            if (deviceJoinInfo is null) throw new ArgumentNullException(nameof(deviceJoinInfo));

            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            // We prioritize the selection of join channel index from reported twin properties (set for OTAA devices)
            // over desired twin properties (set for APB devices).
            switch (deviceJoinInfo.ReportedCN470JoinChannel ?? deviceJoinInfo.DesiredCN470JoinChannel)
            {
                case <= 7: // 20 MHz plan A
                {
                    var channelNumber = upstreamFrequency < Mega(500) ? GetChannelNumber(upstreamFrequency, Mega(470.3)) : 32 + GetChannelNumber(upstreamFrequency, Mega(503.5));
                    downstreamFrequency = this.downstreamFrequenciesByPlanType[0][channelNumber];
                    return true;
                }
                case <= 9: // 20 MHz plan B
                {
                    var channelNumber = upstreamFrequency < Mega(490) ? GetChannelNumber(upstreamFrequency, Mega(476.9)) : 32 + GetChannelNumber(upstreamFrequency, Mega(496.9));
                    downstreamFrequency = this.downstreamFrequenciesByPlanType[1][channelNumber];
                    return true;
                }
                case <= 14: // 26 MHz plan A
                {
                    var channelNumber = GetChannelNumber(upstreamFrequency, Mega(470.3));
                    downstreamFrequency = this.downstreamFrequenciesByPlanType[2][channelNumber % 24];
                    return true;
                }
                case <= 19: // 26 MHz plan B
                {
                    var channelNumber = GetChannelNumber(upstreamFrequency, Mega(480.3));
                    downstreamFrequency = this.downstreamFrequenciesByPlanType[3][channelNumber % 24];
                    return true;
                }
                default:
                    downstreamFrequency = default;
                    return false;
            }

            static int GetChannelNumber(Hertz upstreamChannelFrequency, Hertz startUpstreamFreq) =>
                (int)Math.Round((upstreamChannelFrequency - startUpstreamFreq) / FrequencyIncrement.Units, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo)
        {
            if (deviceJoinInfo is null) throw new ArgumentNullException(nameof(deviceJoinInfo));

            // Default data rate is always 1 for CN470
            var dataRate = DR1;

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
                    return new RX2ReceiveWindow(Mega(492.5), dataRate);
                }
                // 26 MHz plan B
                else if (deviceJoinInfo.ReportedCN470JoinChannel <= 19)
                {
                    return new RX2ReceiveWindow(Mega(502.5), dataRate);
                }
            }

            // ABP device
            else if (deviceJoinInfo.DesiredCN470JoinChannel != null)
            {
                // 20 MHz plan A
                if (deviceJoinInfo.DesiredCN470JoinChannel <= 7)
                {
                    return new RX2ReceiveWindow(Mega(486.9), dataRate);
                }
                // 20 MHz plan B
                else if (deviceJoinInfo.DesiredCN470JoinChannel <= 9)
                {
                    return new RX2ReceiveWindow(Mega(498.3), dataRate);
                }
                // 26 MHz plan A
                else if (deviceJoinInfo.DesiredCN470JoinChannel <= 14)
                {
                    return new RX2ReceiveWindow(Mega(492.5), dataRate);
                }
                // 26 MHz plan B
                else if (deviceJoinInfo.DesiredCN470JoinChannel <= 19)
                {
                    return new RX2ReceiveWindow(Mega(502.5), dataRate);
                }
            }

            return rx2Window;
        }
    }
}
