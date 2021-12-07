// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;

    // Frequency plan for region CN470-510 using version 1 of LoRaWAN 1.0.3 Regional Parameters specification
    public class RegionCN470RP1 : Region
    {
        private const double StartingUpstreamFrequency = 470.3;
        private const double StartingDownstreamFrequency = 500.3;
        private const int DownstreamChannelCount = 48;
        private const double FrequencyIncrement = 0.2;

        public RegionCN470RP1()
            : base(LoRaRegionType.CN470RP1)
        {
            DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(1, (configuration: "SF11BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(2, (configuration: "SF10BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(3, (configuration: "SF9BW125", maxPyldSize: 123));
            DRtoConfiguration.Add(4, (configuration: "SF8BW125", maxPyldSize: 230));
            DRtoConfiguration.Add(5, (configuration: "SF7BW125", maxPyldSize: 230));

            TXPowertoMaxEIRP.Add(0, 19.15);
            TXPowertoMaxEIRP.Add(1, 17.15);
            TXPowertoMaxEIRP.Add(2, 15.15);
            TXPowertoMaxEIRP.Add(3, 13.15);
            TXPowertoMaxEIRP.Add(4, 11.15);
            TXPowertoMaxEIRP.Add(5, 9.15);
            TXPowertoMaxEIRP.Add(6, 7.15);
            TXPowertoMaxEIRP.Add(7, 5.15);

            RX1DROffsetTable = new int[6][]
            {
                new int[] { 0, 0, 0, 0, 0, 0 },
                new int[] { 1, 0, 0, 0, 0, 0 },
                new int[] { 2, 1, 0, 0, 0, 0 },
                new int[] { 3, 2, 1, 0, 0, 0 },
                new int[] { 4, 3, 2, 1, 0, 0 },
                new int[] { 5, 4, 3, 2, 1, 0 }
            };

            var validDatarates = new HashSet<string>()
            {
                "SF12BW125", // 0
                "SF11BW125", // 1
                "SF10BW125", // 2 
                "SF9BW125",  // 3
                "SF8BW125",  // 4
                "SF7BW125"  // 5
            };

            MaxADRDataRate = 5;
            RegionLimits = new RegionLimits((min: 470, max: 510), validDatarates, validDatarates, 0, 0);
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// <param name="upstreamFrequency">The frequency at which the message was transmitted.</param>
        /// <param name="upstreamDataRate">The upstream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        /// </summary>
        public override bool TryGetDownstreamChannelFrequency(double upstreamFrequency, out double downstreamFrequency, ushort? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            var upstreamChannelNumber = (int)Math.Round(
                (upstreamFrequency - StartingUpstreamFrequency) / FrequencyIncrement,
                0,
                MidpointRounding.AwayFromZero);

            downstreamFrequency = Math.Round(
                StartingDownstreamFrequency + ((upstreamChannelNumber % DownstreamChannelCount) * FrequencyIncrement),
                1,
                MidpointRounding.AwayFromZero);

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

            return TryGetDownstreamChannelFrequency(upstreamChannel.Freq, out frequency);
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) => new RX2ReceiveWindow(505.3, 0);
    }
}
