// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using System;
    using System.Collections.Generic;

    public class RegionEU868 : Region
    {
        public RegionEU868()
            : base(LoRaRegionType.EU868)
        {
            DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(1, (configuration: "SF11BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(2, (configuration: "SF10BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(3, (configuration: "SF9BW125", maxPyldSize: 123));
            DRtoConfiguration.Add(4, (configuration: "SF8BW125", maxPyldSize: 230));
            DRtoConfiguration.Add(5, (configuration: "SF7BW125", maxPyldSize: 230));
            DRtoConfiguration.Add(6, (configuration: "SF7BW250", maxPyldSize: 230));
            DRtoConfiguration.Add(7, (configuration: "50", maxPyldSize: 230)); // USED FOR GFSK

            TXPowertoMaxEIRP.Add(0, 16);
            TXPowertoMaxEIRP.Add(1, 14);
            TXPowertoMaxEIRP.Add(2, 12);
            TXPowertoMaxEIRP.Add(3, 10);
            TXPowertoMaxEIRP.Add(4, 8);
            TXPowertoMaxEIRP.Add(5, 6);
            TXPowertoMaxEIRP.Add(6, 4);
            TXPowertoMaxEIRP.Add(7, 2);

            RX1DROffsetTable = new int[8][]
            {
                new int[] { 0, 0, 0, 0, 0, 0 },
                new int[] { 1, 0, 0, 0, 0, 0 },
                new int[] { 2, 1, 0, 0, 0, 0 },
                new int[] { 3, 2, 1, 0, 0, 0 },
                new int[] { 4, 3, 2, 1, 0, 0 },
                new int[] { 5, 4, 3, 2, 1, 0 },
                new int[] { 6, 5, 4, 3, 2, 1 },
                new int[] { 7, 6, 5, 4, 3, 2 }
            };
            var validDataRangeUpAndDownstream = new HashSet<string>()
            {
                "SF12BW125", // 0
                "SF11BW125", // 1
                "SF10BW125", // 2
                "SF9BW125", // 3
                "SF8BW125", // 4
                "SF7BW125", // 5
                "SF7BW250", // 6
                "50" // 7 FSK 50
            };

            MaxADRDataRate = 5;
            RegionLimits = new RegionLimits((min: 863, max: 870), validDataRangeUpAndDownstream, validDataRangeUpAndDownstream, 0, 0);
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region EU868.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public override bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (upstreamChannel is null) throw new ArgumentNullException(nameof(upstreamChannel));

            frequency = 0;

            if (IsValidUpstreamRxpk(upstreamChannel))
            {
                // in case of EU, you respond on same frequency as you sent data.
                frequency = upstreamChannel.Freq;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region EU868.
        /// </summary>
        /// <param name="upstreamFrequency">Frequency on which the message was transmitted.</param>
        /// <param name="dataRate">Data rate at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override bool TryGetDownstreamChannelFrequency(double upstreamFrequency, ushort dataRate, out double downstreamFrequency, DeviceJoinInfo deviceJoinInfo = null)
        {
            downstreamFrequency = 0;

            if (IsValidUpstreamFrequencyAndDataRate(upstreamFrequency, dataRate))
            {
                // in case of EU, you respond on same frequency as you sent data.
                downstreamFrequency = upstreamFrequency;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) => new RX2ReceiveWindow(869.525, 0);
    }
}
