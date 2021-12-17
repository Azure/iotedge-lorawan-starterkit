// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    // Frequency plan for region CN470-510 using version 1 of LoRaWAN 1.0.3 Regional Parameters specification
    public class RegionCN470RP1 : Region
    {
        private static readonly Hertz StartingUpstreamFrequency = Mega(470.3);
        private static readonly Hertz StartingDownstreamFrequency = Mega(500.3);
        private static readonly Mega FrequencyIncrement = new(0.2);

        private const int DownstreamChannelCount = 48;

        public RegionCN470RP1()
            : base(LoRaRegionType.CN470RP1)
        {
            DRtoConfiguration.Add(DR0, (LoRaDataRate.SF12BW125, MaxPayloadSize: 59));
            DRtoConfiguration.Add(DR1, (LoRaDataRate.SF11BW125, MaxPayloadSize: 59));
            DRtoConfiguration.Add(DR2, (LoRaDataRate.SF10BW125, MaxPayloadSize: 59));
            DRtoConfiguration.Add(DR3, (LoRaDataRate.SF9BW125, MaxPayloadSize: 123));
            DRtoConfiguration.Add(DR4, (LoRaDataRate.SF8BW125, MaxPayloadSize: 230));
            DRtoConfiguration.Add(DR5, (LoRaDataRate.SF7BW125, MaxPayloadSize: 230));

            TXPowertoMaxEIRP.Add(0, 19.15);
            TXPowertoMaxEIRP.Add(1, 17.15);
            TXPowertoMaxEIRP.Add(2, 15.15);
            TXPowertoMaxEIRP.Add(3, 13.15);
            TXPowertoMaxEIRP.Add(4, 11.15);
            TXPowertoMaxEIRP.Add(5, 9.15);
            TXPowertoMaxEIRP.Add(6, 7.15);
            TXPowertoMaxEIRP.Add(7, 5.15);

            RX1DROffsetTable = new[]
            {
                new[] { DR0, DR0, DR0, DR0, DR0, DR0 },
                new[] { DR1, DR0, DR0, DR0, DR0, DR0 },
                new[] { DR2, DR1, DR0, DR0, DR0, DR0 },
                new[] { DR3, DR2, DR1, DR0, DR0, DR0 },
                new[] { DR4, DR3, DR2, DR1, DR0, DR0 },
                new[] { DR5, DR4, DR3, DR2, DR1, DR0 },
            };

            var validDatarates = new HashSet<DataRate>
            {
                LoRaDataRate.SF12BW125, // 0
                LoRaDataRate.SF11BW125, // 1
                LoRaDataRate.SF10BW125, // 2
                LoRaDataRate.SF9BW125,  // 3
                LoRaDataRate.SF8BW125,  // 4
                LoRaDataRate.SF7BW125   // 5
            };

            MaxADRDataRate = DR5;
            RegionLimits = new RegionLimits((Min: Mega(470), Max: Mega(510)), validDatarates, validDatarates, 0, 0);
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// <param name="upstreamFrequency">The frequency at which the message was transmitted.</param>
        /// <param name="upstreamDataRate">The upstream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        /// </summary>
        public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, DataRateIndex? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            var upstreamChannelNumber = (int)Math.Round(
                (upstreamFrequency - StartingUpstreamFrequency) / FrequencyIncrement.Units,
                0,
                MidpointRounding.AwayFromZero);

            downstreamFrequency = StartingDownstreamFrequency + checked((long)((upstreamChannelNumber % DownstreamChannelCount) * FrequencyIncrement.Units));

            return true;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamChannel">The channel at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (upstreamChannel is null) throw new ArgumentNullException(nameof(upstreamChannel));

            if (!IsValidUpstreamRxpk(upstreamChannel))
                throw new LoRaProcessingException($"Invalid upstream channel: {upstreamChannel.Freq}, {upstreamChannel.Datr}.");

            (var result, frequency) = TryGetDownstreamChannelFrequency(upstreamChannel.FreqHertz, out var downstream)
                                    ? (true, downstream.InMega) : default;
            return result;
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) => new RX2ReceiveWindow(Mega(505.3), 0);
    }
}
