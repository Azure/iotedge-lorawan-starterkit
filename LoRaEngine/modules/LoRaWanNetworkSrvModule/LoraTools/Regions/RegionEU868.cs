// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using System;
    using System.Collections.Generic;
    using static LoRaWan.DataRateIndex;
    using static LoRaWan.Metric;

    public class RegionEU868 : Region
    {
        public RegionEU868()
            : base(LoRaRegionType.EU868)
        {
            DRtoConfiguration.Add(DR0, (LoRaDataRate.SF12BW125, MaxPayloadSize: 59));
            DRtoConfiguration.Add(DR1, (LoRaDataRate.SF11BW125, MaxPayloadSize: 59));
            DRtoConfiguration.Add(DR2, (LoRaDataRate.SF10BW125, MaxPayloadSize: 59));
            DRtoConfiguration.Add(DR3, (LoRaDataRate.SF9BW125, MaxPayloadSize: 123));
            DRtoConfiguration.Add(DR4, (LoRaDataRate.SF8BW125, MaxPayloadSize: 230));
            DRtoConfiguration.Add(DR5, (LoRaDataRate.SF7BW125, MaxPayloadSize: 230));
            DRtoConfiguration.Add(DR6, (LoRaDataRate.SF7BW250, MaxPayloadSize: 230));
            DRtoConfiguration.Add(DR7, (FskDataRate.Fsk50000, MaxPayloadSize: 230));

            TXPowertoMaxEIRP.Add(0, 16);
            TXPowertoMaxEIRP.Add(1, 14);
            TXPowertoMaxEIRP.Add(2, 12);
            TXPowertoMaxEIRP.Add(3, 10);
            TXPowertoMaxEIRP.Add(4, 8);
            TXPowertoMaxEIRP.Add(5, 6);
            TXPowertoMaxEIRP.Add(6, 4);
            TXPowertoMaxEIRP.Add(7, 2);

            RX1DROffsetTable = new[]
            {
                new[] { DR0, DR0, DR0, DR0, DR0, DR0 },
                new[] { DR1, DR0, DR0, DR0, DR0, DR0 },
                new[] { DR2, DR1, DR0, DR0, DR0, DR0 },
                new[] { DR3, DR2, DR1, DR0, DR0, DR0 },
                new[] { DR4, DR3, DR2, DR1, DR0, DR0 },
                new[] { DR5, DR4, DR3, DR2, DR1, DR0 },
                new[] { DR6, DR5, DR4, DR3, DR2, DR1 },
                new[] { DR7, DR6, DR5, DR4, DR3, DR2 }
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

            MaxADRDataRate = DR5;
            RegionLimits = new RegionLimits((Min: Mega(863), Max: Mega(870)), validDataRangeUpAndDownstream, validDataRangeUpAndDownstream, 0, 0);
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

            if (!IsValidUpstreamRxpk(upstreamChannel))
                throw new LoRaProcessingException($"Invalid upstream channel: {upstreamChannel.Freq}, {upstreamChannel.Datr}.");

            // in case of EU, you respond on same frequency as you sent data.
            frequency = upstreamChannel.Freq;
            return true;
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
