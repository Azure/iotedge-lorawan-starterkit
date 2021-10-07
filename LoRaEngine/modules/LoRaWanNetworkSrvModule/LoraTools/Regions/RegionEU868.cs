// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.LoRaPhysical;

    public class RegionEU868 : Region
    {
        public RegionEU868(byte loRaSyncWord, byte[] gFSKSyncWord, (double frequency, ushort datr) rx2DefaultReceiveWindows, uint receive_delay1, uint receive_delay2, uint join_accept_delay1, uint join_accept_delay2, int max_fcnt_gap, uint adr_ack_limit, uint adr_adr_delay, (uint min, uint max) ack_timeout)
            : base(LoRaRegionType.EU868, loRaSyncWord, gFSKSyncWord, rx2DefaultReceiveWindows, receive_delay1, receive_delay2, join_accept_delay1, join_accept_delay2, max_fcnt_gap, adr_ack_limit, adr_adr_delay, ack_timeout)
        {
        }

        /// <summary>
        /// Logic to get the correct transmission frequency for region EU868.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        public override bool TryGetUpstreamChannelFrequency(Rxpk upstreamChannel, out double frequency)
        {
            frequency = 0;

            if (this.IsValidUpstreamRxpk(upstreamChannel))
            {
                // in case of EU, you respond on same frequency as you sent data.
                frequency = upstreamChannel.Freq;
                return true;
            }

            return false;
        }
    }
}
