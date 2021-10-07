// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using LoRaTools.LoRaPhysical;

    public class RegionUS915 : Region
    {
        public RegionUS915(byte loRaSyncWord, byte[] gFSKSyncWord, (double frequency, ushort datr) rx2DefaultReceiveWindows, uint receive_delay1, uint receive_delay2, uint join_accept_delay1, uint join_accept_delay2, int max_fcnt_gap, uint adr_ack_limit, uint adr_adr_delay, (uint min, uint max) ack_timeout)
            : base(LoRaRegionType.US915, loRaSyncWord, gFSKSyncWord, rx2DefaultReceiveWindows, receive_delay1, receive_delay2, join_accept_delay1, join_accept_delay2, max_fcnt_gap, adr_ack_limit, adr_adr_delay, ack_timeout)
        {
        }

        /// <summary>
        /// Logic to get the correct transmission frequency for region US915.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        public override bool TryGetUpstreamChannelFrequency(Rxpk upstreamChannel, out double frequency)
        {
            frequency = 0;

            if (this.IsValidUpstreamRxpk(upstreamChannel))
            {
                int upstreamChannelNumber;
                // if DR4 the coding are different.
                if (upstreamChannel.Datr == "SF8BW500")
                {
                    // ==DR4
                    upstreamChannelNumber = 64 + (int)Math.Round((upstreamChannel.Freq - 903) / 1.6, 0);
                }
                else
                {
                    // if not DR4 other encoding
                    upstreamChannelNumber = (int)((upstreamChannel.Freq - 902.3) / 0.2);
                }

                frequency = Math.Round(923.3 + upstreamChannelNumber % 8 * 0.6, 1);
                return true;
            }

            return false;
        }
    }
}
