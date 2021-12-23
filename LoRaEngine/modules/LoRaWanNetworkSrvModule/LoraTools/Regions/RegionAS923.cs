// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Configuration;
    using LoRaTools.Utils;
    using LoRaWan;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public class RegionAS923 : DwellTimeLimitedRegion
    {
        private static readonly Hertz Channel0Frequency = Mega(923.2);
        private static readonly Hertz Channel1Frequency = Mega(923.4);

        private static readonly ImmutableDictionary<DataRateIndex, (DataRate configuration, uint maxPyldSize)> DrToNoDwell =
            new Dictionary<DataRateIndex, (DataRate configuration, uint maxPyldSize)>
            {
                [DR0] = (LoRaDataRate.SF12BW125, 59),
                [DR1] = (LoRaDataRate.SF11BW125, 59),
                [DR2] = (LoRaDataRate.SF10BW125, 123),
                [DR3] = (LoRaDataRate.SF9BW125, 123),
                [DR4] = (LoRaDataRate.SF8BW125, 230),
                [DR5] = (LoRaDataRate.SF7BW125, 230),
                [DR6] = (LoRaDataRate.SF7BW250, 230),
                [DR7] = (FskDataRate.Fsk50000, 230)
            }.ToImmutableDictionary();

        private static readonly ImmutableHashSet<DataRate> ValidDataRatesDr0Dr7 =
            ImmutableHashSet.Create<DataRate>(LoRaDataRate.SF12BW125,
                                              LoRaDataRate.SF11BW125,
                                              LoRaDataRate.SF10BW125,
                                              LoRaDataRate.SF9BW125,
                                              LoRaDataRate.SF8BW125,
                                              LoRaDataRate.SF7BW125,
                                              LoRaDataRate.SF7BW250,
                                              FskDataRate.Fsk50000);

        private static readonly ImmutableArray<IReadOnlyList<DataRateIndex>> RX1DROffsetTableNoDwell =
            new IReadOnlyList<DataRateIndex>[]
            {
                new DataRateIndex[] { DR0, DR0, DR0, DR0, DR0, DR0, DR1, DR2 }.ToImmutableArray(),
                new DataRateIndex[] { DR1, DR0, DR0, DR0, DR0, DR0, DR2, DR3 }.ToImmutableArray(),
                new DataRateIndex[] { DR2, DR1, DR0, DR0, DR0, DR0, DR3, DR4 }.ToImmutableArray(),
                new DataRateIndex[] { DR3, DR2, DR1, DR0, DR0, DR0, DR4, DR5 }.ToImmutableArray(),
                new DataRateIndex[] { DR4, DR3, DR2, DR1, DR0, DR0, DR5, DR6 }.ToImmutableArray(),
                new DataRateIndex[] { DR5, DR4, DR3, DR2, DR1, DR0, DR6, DR7 }.ToImmutableArray(),
                new DataRateIndex[] { DR6, DR5, DR4, DR3, DR2, DR1, DR7, DR7 }.ToImmutableArray(),
                new DataRateIndex[] { DR7, DR6, DR5, DR4, DR3, DR2, DR7, DR7 }.ToImmutableArray(),
            }.ToImmutableArray();

        private static readonly RegionLimits RegionLimitsNoDwell =
            new RegionLimits((Min: Mega(915), Max: Mega(928)), ValidDataRatesDr0Dr7, ValidDataRatesDr0Dr7, DR0, DR0);

        private static readonly ImmutableDictionary<DataRateIndex, (DataRate configuration, uint maxPyldSize)> DrToWithDwell =
            new Dictionary<DataRateIndex, (DataRate configuration, uint maxPyldSize)>
            {
                [DR0] = (LoRaDataRate.SF12BW125, 0),
                [DR1] = (LoRaDataRate.SF11BW125, 0),
                [DR2] = (LoRaDataRate.SF10BW125, 19),
                [DR3] = (LoRaDataRate.SF9BW125, 61),
                [DR4] = (LoRaDataRate.SF8BW125, 133),
                [DR5] = (LoRaDataRate.SF7BW125, 230),
                [DR6] = (LoRaDataRate.SF7BW250, 230),
                [DR7] = (FskDataRate.Fsk50000, 230)
            }.ToImmutableDictionary();

        private static readonly ImmutableArray<IReadOnlyList<DataRateIndex>> RX1DROffsetTableWithDwell =
            new IReadOnlyList<DataRateIndex>[]
            {
                new DataRateIndex[] { DR2, DR2, DR2, DR2, DR2, DR2, DR2, DR2 }.ToImmutableArray(),
                new DataRateIndex[] { DR2, DR2, DR2, DR2, DR2, DR2, DR2, DR3 }.ToImmutableArray(),
                new DataRateIndex[] { DR2, DR2, DR2, DR2, DR2, DR2, DR3, DR4 }.ToImmutableArray(),
                new DataRateIndex[] { DR3, DR2, DR2, DR2, DR2, DR2, DR4, DR5 }.ToImmutableArray(),
                new DataRateIndex[] { DR4, DR3, DR2, DR2, DR2, DR2, DR5, DR6 }.ToImmutableArray(),
                new DataRateIndex[] { DR5, DR4, DR3, DR2, DR2, DR2, DR6, DR7 }.ToImmutableArray(),
                new DataRateIndex[] { DR6, DR5, DR4, DR3, DR2, DR2, DR7, DR7 }.ToImmutableArray(),
                new DataRateIndex[] { DR7, DR6, DR5, DR4, DR3, DR2, DR7, DR7 }.ToImmutableArray(),
            }.ToImmutableArray();

        private static readonly ImmutableHashSet<DataRate> ValidDataRatesDr2Dr7 =
            ImmutableHashSet.Create<DataRate>(LoRaDataRate.SF10BW125,
                                              LoRaDataRate.SF9BW125,
                                              LoRaDataRate.SF8BW125,
                                              LoRaDataRate.SF7BW125,
                                              LoRaDataRate.SF7BW250,
                                              FskDataRate.Fsk50000);

        private static readonly RegionLimits RegionLimitsWithDwell =
            new RegionLimits((Min: Mega(915), Max: Mega(928)), ValidDataRatesDr0Dr7, ValidDataRatesDr2Dr7, DR0, DR2);

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
                [7] = 2
            }.ToImmutableDictionary();

        public override IDictionary<uint, double> TXPowertoMaxEIRP => MaxEirpByTxPower;

        private DwellTimeSetting dwellTimeSetting;

        public override IDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DRtoConfiguration =>
            ApplyDwellTimeLimits ? DrToWithDwell : DrToNoDwell;

        public override IReadOnlyList<IReadOnlyList<DataRateIndex>> RX1DROffsetTable =>
            ApplyDwellTimeLimits ? RX1DROffsetTableWithDwell : RX1DROffsetTableNoDwell;

        protected override DwellTimeSetting DefaultDwellTimeSetting { get; } = new DwellTimeSetting(DownlinkDwellTime: true, UplinkDwellTime: true, 5);

        private bool ApplyDwellTimeLimits => (this.dwellTimeSetting ?? DefaultDwellTimeSetting).DownlinkDwellTime;

        public long FrequencyOffset { get; private set; }

        public RegionAS923()
            : base(LoRaRegionType.AS923)
        {
            FrequencyOffset = 0;
            MaxADRDataRate = DR7;
            RegionLimits = ApplyDwellTimeLimits ? RegionLimitsWithDwell : RegionLimitsNoDwell;
        }

        /// <summary>
        /// Calculates the frequency offset (AS923_FREQ_OFFSET_HZ) value for region AS923.
        /// </summary>
        /// <param name="frequencyChannel0">Configured frequency for radio 0.</param>
        /// <param name="frequencyChannel1">Configured frequency for radio 1.</param>
        public RegionAS923 WithFrequencyOffset(Hertz frequencyChannel0, Hertz frequencyChannel1)
        {
            FrequencyOffset = frequencyChannel0 - Channel0Frequency;

            var channel1Offset = frequencyChannel1 - Channel1Frequency;
            if (channel1Offset != FrequencyOffset)
            {
                throw new ConfigurationErrorsException($"Provided channel frequencies {frequencyChannel0}, {frequencyChannel1} for Region {LoRaRegion} are inconsistent.");
            }

            return this;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamFrequency">The frequency at which the message was transmitted.</param>
        /// <param name="upstreamDataRate">The upstream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, DataRateIndex? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            // Use the same frequency as the upstream.
            downstreamFrequency = upstreamFrequency;
            return true;
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) =>
            new RX2ReceiveWindow(Hertz.Mega(923.2) + FrequencyOffset, DR2);

        /// <inheritdoc/>
        public override void UseDwellTimeSetting(DwellTimeSetting dwellTimeSetting)
        {
            this.dwellTimeSetting = dwellTimeSetting;
            RegionLimits = ApplyDwellTimeLimits ? RegionLimitsWithDwell : RegionLimitsNoDwell;
        }
    }
}
