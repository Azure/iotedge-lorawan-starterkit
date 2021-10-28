// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using System;

    public class RegionEU868 : Region
    {
        public RegionEU868()
            : base(LoRaRegionType.EU868)
        {
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region EU868.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
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
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) =>
            new RX2ReceiveWindow { Frequency = 869.525, DataRate = 0 };
    }
}
