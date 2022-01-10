// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using LoRaTools.Utils;
    using LoRaWan;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public class RegionUS915 : Region
    {
        // Frequencies calculated according to formula:
        // 923.3 + upstreamChannelNumber % 8 * 0.6,
        // rounded to first decimal point
        private static readonly Hertz[] DownstreamChannelFrequencies =
        {
            Mega(923.3),
            Mega(923.9),
            Mega(924.5),
            Mega(925.1),
            Mega(925.7),
            Mega(926.3),
            Mega(926.9),
            Mega(927.5)
        };

        private static readonly ImmutableDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DrToConfigurationByDrIndex =
            new Dictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)>
            {
                [DR0] = (LoRaDataRate.SF10BW125, MaxPayloadSize: 19),
                [DR1] = (LoRaDataRate.SF9BW125, MaxPayloadSize: 61),
                [DR2] = (LoRaDataRate.SF8BW125, MaxPayloadSize: 133),
                [DR3] = (LoRaDataRate.SF7BW125, MaxPayloadSize: 250),
                [DR4] = (LoRaDataRate.SF8BW500, MaxPayloadSize: 250),
                [DR8] = (LoRaDataRate.SF12BW500, MaxPayloadSize: 61),
                [DR9] = (LoRaDataRate.SF11BW500, MaxPayloadSize: 137),
                [DR10] = (LoRaDataRate.SF10BW500, MaxPayloadSize: 250),
                [DR11] = (LoRaDataRate.SF9BW500, MaxPayloadSize: 250),
                [DR12] = (LoRaDataRate.SF8BW500, MaxPayloadSize: 250),
                [DR13] = (LoRaDataRate.SF7BW500, MaxPayloadSize: 250),
            }.ToImmutableDictionary();

        public override IReadOnlyDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DRtoConfiguration => DrToConfigurationByDrIndex;

        private static readonly ImmutableDictionary<uint, double> MaxEirpByTxPower =
            new Dictionary<uint, double>
            {
                [0] = 30,
                [1] = 29,
                [2] = 28,
                [3] = 27,
                [4] = 26,
                [5] = 25,
                [6] = 24,
                [7] = 23,
                [8] = 22,
                [9] = 21,
                [10] = 20,
                [11] = 19,
                [12] = 18,
                [13] = 17,
            }.ToImmutableDictionary();

        public override IReadOnlyDictionary<uint, double> TXPowertoMaxEIRP => MaxEirpByTxPower;

        private static readonly ImmutableArray<IReadOnlyList<DataRateIndex>> RX1DROffsetTableInternal =
            new IReadOnlyList<DataRateIndex>[]
            {
                new[] { DR10, DR9,  DR8,  DR8  }.ToImmutableArray(),
                new[] { DR11, DR10, DR9,  DR8  }.ToImmutableArray(),
                new[] { DR12, DR11, DR10, DR9  }.ToImmutableArray(),
                new[] { DR13, DR12, DR11, DR10 }.ToImmutableArray(),
                new[] { DR13, DR13, DR12, DR11 }.ToImmutableArray(),
            }.ToImmutableArray();

        public override IReadOnlyList<IReadOnlyList<DataRateIndex>> RX1DROffsetTable => RX1DROffsetTableInternal;

        public RegionUS915()
            : base(LoRaRegionType.US915)
        {
            var upstreamValidDataranges = new HashSet<DataRate>
            {
                LoRaDataRate.SF10BW125, // 0
                LoRaDataRate.SF9BW125,  // 1
                LoRaDataRate.SF8BW125,  // 2
                LoRaDataRate.SF7BW125,  // 3
                LoRaDataRate.SF8BW500,  // 4
            };

            var downstreamValidDataranges = new HashSet<DataRate>
            {
                LoRaDataRate.SF12BW500, // 8
                LoRaDataRate.SF11BW500, // 9
                LoRaDataRate.SF10BW500, // 10
                LoRaDataRate.SF9BW500,  // 11
                LoRaDataRate.SF8BW500,  // 12
                LoRaDataRate.SF7BW500   // 13
            };

            MaxADRDataRate = DR3;
            RegionLimits = new RegionLimits((Min: Mega(902.3), Max: Mega(927.5)), upstreamValidDataranges, downstreamValidDataranges, DR0, DR8);
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region US915.
        /// </summary>
        /// <param name="upstreamFrequency">Frequency on which the message was transmitted.</param>
        /// <param name="upstreamDataRate">Data rate at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, DataRateIndex? upstreamDataRate, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (upstreamDataRate is null) throw new ArgumentNullException(nameof(upstreamDataRate));

            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            if (!IsValidUpstreamDataRate(upstreamDataRate.Value))
                throw new LoRaProcessingException($"Invalid upstream data rate {upstreamDataRate}", LoRaProcessingErrorCode.InvalidDataRate);

            int upstreamChannelNumber;
            upstreamChannelNumber = upstreamDataRate == DR4 ? 64 + (int)Math.Round((upstreamFrequency.InMega - 903) / 1.6, 0, MidpointRounding.AwayFromZero)
                                                            : (int)Math.Round((upstreamFrequency.InMega - 902.3) / 0.2, 0, MidpointRounding.AwayFromZero);
            downstreamFrequency = DownstreamChannelFrequencies[upstreamChannelNumber % 8];
            return true;
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) => new RX2ReceiveWindow(Mega(923.3), DR8);
    }
}
