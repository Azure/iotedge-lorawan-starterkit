// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using static LoRaWan.Metric;

    public class RegionUS915 : Region
    {
        // Frequencies calculated according to formula:
        // 923.3 + upstreamChannelNumber % 8 * 0.6,
        // rounded to first decimal point
        private static readonly Hertz[] DownstreamChannelFrequencies = new Hertz[]
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

        public RegionUS915()
            : base(LoRaRegionType.US915)
        {
            DRtoConfiguration.Add(0, (configuration: "SF10BW125", maxPyldSize: 19));
            DRtoConfiguration.Add(1, (configuration: "SF9BW125", maxPyldSize: 61));
            DRtoConfiguration.Add(2, (configuration: "SF8BW125", maxPyldSize: 133));
            DRtoConfiguration.Add(3, (configuration: "SF7BW125", maxPyldSize: 250));
            DRtoConfiguration.Add(4, (configuration: "SF8BW500", maxPyldSize: 250));
            DRtoConfiguration.Add(8, (configuration: "SF12BW500", maxPyldSize: 61));
            DRtoConfiguration.Add(9, (configuration: "SF11BW500", maxPyldSize: 137));
            DRtoConfiguration.Add(10, (configuration: "SF10BW500", maxPyldSize: 250));
            DRtoConfiguration.Add(11, (configuration: "SF9BW500", maxPyldSize: 250));
            DRtoConfiguration.Add(12, (configuration: "SF8BW500", maxPyldSize: 250));
            DRtoConfiguration.Add(13, (configuration: "SF7BW500", maxPyldSize: 250));

            for (uint i = 0; i < 14; i++)
            {
                TXPowertoMaxEIRP.Add(i, 30 - i);
            }

            RX1DROffsetTable = new int[5][]
            {
                new int[] { 10, 9, 8, 8 },
                new int[] { 11, 10, 9, 8 },
                new int[] { 12, 11, 10, 9 },
                new int[] { 13, 12, 11, 10 },
                new int[] { 13, 13, 12, 11 },
            };

            var upstreamValidDataranges = new HashSet<string>()
            {
                "SF10BW125", // 0
                "SF9BW125", // 1
                "SF8BW125", // 2
                "SF7BW125", // 3
                "SF8BW500", // 4
            };

            var downstreamValidDataranges = new HashSet<string>()
            {
                "SF12BW500", // 8
                "SF11BW500", // 9
                "SF10BW500", // 10
                "SF9BW500", // 11
                "SF8BW500", // 12
                "SF7BW500" // 13
            };

            MaxADRDataRate = 3;
            RegionLimits = new RegionLimits((Min: Mega(902.3), Max: Mega(927.5)), upstreamValidDataranges, downstreamValidDataranges, 0, 8);
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region US915.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (upstreamChannel is null) throw new ArgumentNullException(nameof(upstreamChannel));

            if (!IsValidUpstreamRxpk(upstreamChannel))
                throw new LoRaProcessingException($"Invalid upstream channel: {upstreamChannel.Freq}, {upstreamChannel.Datr}.");

            int upstreamChannelNumber;
            // if DR4 the coding are different.
            if (upstreamChannel.Datr == "SF8BW500")
            {
                // ==DR4
                upstreamChannelNumber = 64 + (int)Math.Round((upstreamChannel.Freq - 903) / 1.6, 0, MidpointRounding.AwayFromZero);
            }
            else
            {
                // if not DR4 other encoding
                upstreamChannelNumber = (int)Math.Round((upstreamChannel.Freq - 902.3) / 0.2, 0, MidpointRounding.AwayFromZero);
            }

            frequency = DownstreamChannelFrequencies[upstreamChannelNumber % 8].InMega;
            return true;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region US915.
        /// </summary>
        /// <param name="upstreamFrequency">Frequency on which the message was transmitted.</param>
        /// <param name="upstreamDataRate">Data rate at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, ushort? upstreamDataRate, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (upstreamDataRate is null) throw new ArgumentNullException(nameof(upstreamDataRate));

            if (!IsValidUpstreamFrequency(upstreamFrequency))
                throw new LoRaProcessingException($"Invalid upstream frequency {upstreamFrequency}", LoRaProcessingErrorCode.InvalidFrequency);

            if (!IsValidUpstreamDataRate((ushort)upstreamDataRate))
                throw new LoRaProcessingException($"Invalid upstream data rate {upstreamDataRate}", LoRaProcessingErrorCode.InvalidDataRate);

            int upstreamChannelNumber;
            upstreamChannelNumber = upstreamDataRate == 4 ? 64 + (int)Math.Round((upstreamFrequency.InMega - 903) / 1.6, 0, MidpointRounding.AwayFromZero)
                                                    : (int)Math.Round((upstreamFrequency.InMega - 902.3) / 0.2, 0, MidpointRounding.AwayFromZero);
            downstreamFrequency = DownstreamChannelFrequencies[upstreamChannelNumber % 8];
            return true;
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) => new RX2ReceiveWindow(Mega(923.3), 8);
    }
}
