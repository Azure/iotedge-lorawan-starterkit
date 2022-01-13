// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.Utils;
    using LoRaWan;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public class RegionEU868 : Region
    {
        private static readonly ImmutableDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DrToConfigurationByDrIndex =
            new Dictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)>
            {
                [DR0] = (LoRaDataRate.SF12BW125, MaxPayloadSize: 59),
                [DR1] = (LoRaDataRate.SF11BW125, MaxPayloadSize: 59),
                [DR2] = (LoRaDataRate.SF10BW125, MaxPayloadSize: 59),
                [DR3] = (LoRaDataRate.SF9BW125, MaxPayloadSize: 123),
                [DR4] = (LoRaDataRate.SF8BW125, MaxPayloadSize: 230),
                [DR5] = (LoRaDataRate.SF7BW125, MaxPayloadSize: 230),
                [DR6] = (LoRaDataRate.SF7BW250, MaxPayloadSize: 230),
                [DR7] = (FskDataRate.Fsk50000, MaxPayloadSize: 230),
            }.ToImmutableDictionary();

        public override IReadOnlyDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DRtoConfiguration => DrToConfigurationByDrIndex;

        private static readonly ImmutableDictionary<uint, double> MaxEirpByTxPower =
            new Dictionary<uint, double>
            {
                [0] = 16,
                [1] = 14,
                [2] = 12,
                [3] = 10,
                [4] = 8,
                [5] = 6,
                [6] = 4,
                [7] = 2,
            }.ToImmutableDictionary();

        public override IReadOnlyDictionary<uint, double> TXPowertoMaxEIRP => MaxEirpByTxPower;

        private static readonly ImmutableArray<IReadOnlyList<DataRateIndex>> RX1DROffsetTableInternal =
            new IReadOnlyList<DataRateIndex>[]
            {
                new[] { DR0, DR0, DR0, DR0, DR0, DR0 }.ToImmutableArray(),
                new[] { DR1, DR0, DR0, DR0, DR0, DR0 }.ToImmutableArray(),
                new[] { DR2, DR1, DR0, DR0, DR0, DR0 }.ToImmutableArray(),
                new[] { DR3, DR2, DR1, DR0, DR0, DR0 }.ToImmutableArray(),
                new[] { DR4, DR3, DR2, DR1, DR0, DR0 }.ToImmutableArray(),
                new[] { DR5, DR4, DR3, DR2, DR1, DR0 }.ToImmutableArray(),
                new[] { DR6, DR5, DR4, DR3, DR2, DR1 }.ToImmutableArray(),
                new[] { DR7, DR6, DR5, DR4, DR3, DR2 }.ToImmutableArray(),
            }.ToImmutableArray();

        public override IReadOnlyList<IReadOnlyList<DataRateIndex>> RX1DROffsetTable => RX1DROffsetTableInternal;

        public RegionEU868()
            : base(LoRaRegionType.EU868)
        {
            var validDataRangeUpAndDownstream = new HashSet<DataRate>
            {
                LoRaDataRate.SF12BW125, // 0
                LoRaDataRate.SF11BW125, // 1
                LoRaDataRate.SF10BW125, // 2
                LoRaDataRate.SF9BW125,  // 3
                LoRaDataRate.SF8BW125,  // 4
                LoRaDataRate.SF7BW125,  // 5
                LoRaDataRate.SF7BW250,  // 6
                FskDataRate.Fsk50000    // 7
            };

            MaxADRDataRate = DR5;
            RegionLimits = new RegionLimits((Min: Mega(863), Max: Mega(870)), validDataRangeUpAndDownstream, validDataRangeUpAndDownstream, 0, 0);
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region EU868.
        /// </summary>
        /// <param name="upstreamFrequency">Frequency on which the message was transmitted.</param>
        /// <param name="upstreamDataRate">Data rate at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, DataRateIndex? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            // in case of EU, you respond on same frequency as you sent data.
            downstreamFrequency = upstreamFrequency;
            return true;
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) => new RX2ReceiveWindow(Mega(869.525), 0);
    }
}
