// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public class RegionAS923 : DwellTimeLimitedRegion
    {
        private static readonly Hertz Channel0Frequency = Mega(923.2);
        private static readonly Hertz Channel1Frequency = Mega(923.4);

        private static readonly Dictionary<DataRateIndex, (DataRate configuration, uint maxPyldSize)> DrToNoDwell =
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
            };

        private static readonly IReadOnlyList<IReadOnlyList<DataRateIndex>> RX1DROffsetTableNoDwell =
            new DataRateIndex[8][]
            {
                new DataRateIndex[] { DR0, DR0, DR0, DR0, DR0, DR0, DR1, DR2 },
                new DataRateIndex[] { DR1, DR0, DR0, DR0, DR0, DR0, DR2, DR3 },
                new DataRateIndex[] { DR2, DR1, DR0, DR0, DR0, DR0, DR3, DR4 },
                new DataRateIndex[] { DR3, DR2, DR1, DR0, DR0, DR0, DR4, DR5 },
                new DataRateIndex[] { DR4, DR3, DR2, DR1, DR0, DR0, DR5, DR6 },
                new DataRateIndex[] { DR5, DR4, DR3, DR2, DR1, DR0, DR6, DR7 },
                new DataRateIndex[] { DR6, DR5, DR4, DR3, DR2, DR1, DR7, DR7 },
                new DataRateIndex[] { DR7, DR6, DR5, DR4, DR3, DR2, DR7, DR7 },
            };

        private static readonly Dictionary<DataRateIndex, (DataRate configuration, uint maxPyldSize)> DrToWithDwell =
            new Dictionary<DataRateIndex, (DataRate configuration, uint maxPyldSize)>
            {
                [DR0] = (LoRaDataRate.SF12BW125, 59),
                [DR1] = (LoRaDataRate.SF11BW125, 59),
                [DR2] = (LoRaDataRate.SF10BW125, 19),
                [DR3] = (LoRaDataRate.SF10BW125, 61),
                [DR4] = (LoRaDataRate.SF10BW125, 133),
                [DR5] = (LoRaDataRate.SF7BW125, 230),
                [DR6] = (LoRaDataRate.SF7BW250, 230),
                [DR7] = (FskDataRate.Fsk50000, 230)
            };

        private static readonly IReadOnlyList<IReadOnlyList<DataRateIndex>> RX1DROffsetTableWithDwell =
            new DataRateIndex[8][]
            {
                new DataRateIndex[] { DR2, DR2, DR2, DR2, DR2, DR2, DR2, DR2 },
                new DataRateIndex[] { DR2, DR2, DR2, DR2, DR2, DR2, DR2, DR3 },
                new DataRateIndex[] { DR2, DR2, DR2, DR2, DR2, DR2, DR3, DR4 },
                new DataRateIndex[] { DR3, DR2, DR2, DR2, DR2, DR2, DR4, DR5 },
                new DataRateIndex[] { DR4, DR3, DR2, DR2, DR2, DR2, DR5, DR6 },
                new DataRateIndex[] { DR5, DR4, DR3, DR2, DR2, DR2, DR6, DR7 },
                new DataRateIndex[] { DR6, DR5, DR4, DR3, DR2, DR2, DR7, DR7 },
                new DataRateIndex[] { DR7, DR6, DR5, DR4, DR3, DR2, DR7, DR7 },
            };


        private DwellTimeSetting dwellTimeSetting = new DwellTimeSetting(false, false, 0);

        public override Dictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DRtoConfiguration =>
            this.dwellTimeSetting.DownlinkDwellTime ? DrToWithDwell : DrToNoDwell;

        public long FrequencyOffset { get; private set; }

        public RegionAS923()
            : base(LoRaRegionType.AS923)
        {
            FrequencyOffset = 0;

            TXPowertoMaxEIRP.Add(0, 16);
            TXPowertoMaxEIRP.Add(1, 14);
            TXPowertoMaxEIRP.Add(2, 12);
            TXPowertoMaxEIRP.Add(3, 10);
            TXPowertoMaxEIRP.Add(4, 8);
            TXPowertoMaxEIRP.Add(5, 6);
            TXPowertoMaxEIRP.Add(6, 4);
            TXPowertoMaxEIRP.Add(7, 2);

            var validDatarates = new HashSet<DataRate>
            {
                LoRaDataRate.SF12BW125,
                LoRaDataRate.SF11BW125,
                LoRaDataRate.SF10BW125,
                LoRaDataRate.SF9BW125,
                LoRaDataRate.SF8BW125,
                LoRaDataRate.SF7BW125,
                LoRaDataRate.SF7BW250,
                FskDataRate.Fsk50000,
            };

            MaxADRDataRate = DR7;
            RegionLimits = new RegionLimits((Min: Mega(915), Max: Mega(928)), validDatarates, validDatarates, 0, 0);
            UseDwellTimeSetting(this.dwellTimeSetting);
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
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, DeviceJoinInfo deviceJoinInfo)
        {
            if (upstreamChannel is null) throw new ArgumentNullException(nameof(upstreamChannel));

            if (!IsValidUpstreamRxpk(upstreamChannel))
                throw new LoRaProcessingException($"Invalid upstream channel: {upstreamChannel.Freq}, {upstreamChannel.Datr}.");

            // Use the same frequency as the upstream.
            frequency = upstreamChannel.Freq;
            return true;
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
            RX1DROffsetTable = this.dwellTimeSetting.DownlinkDwellTime ? RX1DROffsetTableWithDwell : RX1DROffsetTableNoDwell;
        }
    }
}
