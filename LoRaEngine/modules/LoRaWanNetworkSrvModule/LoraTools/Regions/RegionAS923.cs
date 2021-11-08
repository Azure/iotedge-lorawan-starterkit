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

    public class RegionAS923 : Region
    {
        private const double Channel0Frequency = 923.2;
        private const double Channel1Frequency = 923.4;
        private const double Epsilon = 0.001;

        private readonly bool useDwellTimeLimit;

        public double FrequencyOffset { get; private set; }

        public RegionAS923(int dwellTime = 0)
            : base(LoRaRegionType.AS923)
        {
            if (dwellTime is not 0 and not 1)
            {
                throw new ConfigurationErrorsException($"Incorrect DwellTime parameter value {dwellTime}; allowed values are 0 or 1.");
            }

            useDwellTimeLimit = dwellTime == 1;

            FrequencyOffset = 0;

            // Values assuming FOpts param is used
            DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(1, (configuration: "SF11BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(2, (configuration: "SF10BW125", maxPyldSize: 123));
            DRtoConfiguration.Add(3, (configuration: "SF9BW125", maxPyldSize: 123));
            DRtoConfiguration.Add(4, (configuration: "SF8BW125", maxPyldSize: 230));
            DRtoConfiguration.Add(5, (configuration: "SF7BW125", maxPyldSize: 230));
            DRtoConfiguration.Add(6, (configuration: "SF7BW250", maxPyldSize: 230));
            DRtoConfiguration.Add(7, (configuration: "50", maxPyldSize: 230)); // FSK 50

            if (useDwellTimeLimit)
            {
                DRtoConfiguration[2] = (configuration: "SF10BW125", maxPyldSize: 19);
                DRtoConfiguration[3] = (configuration: "SF10BW125", maxPyldSize: 61);
                DRtoConfiguration[4] = (configuration: "SF10BW125", maxPyldSize: 133);
            }

            TXPowertoMaxEIRP.Add(0, 16);
            TXPowertoMaxEIRP.Add(1, 14);
            TXPowertoMaxEIRP.Add(2, 12);
            TXPowertoMaxEIRP.Add(3, 10);
            TXPowertoMaxEIRP.Add(4, 8);
            TXPowertoMaxEIRP.Add(5, 6);
            TXPowertoMaxEIRP.Add(6, 4);
            TXPowertoMaxEIRP.Add(7, 2);

            RX1DROffsetTable = useDwellTimeLimit
                ? new int[8][]
                {
                    new int[] { 2, 2, 2, 2, 2, 2, 2, 2 },
                    new int[] { 2, 2, 2, 2, 2, 2, 2, 3 },
                    new int[] { 2, 2, 2, 2, 2, 2, 3, 4 },
                    new int[] { 3, 2, 2, 2, 2, 2, 4, 5 },
                    new int[] { 4, 3, 2, 2, 2, 2, 5, 6 },
                    new int[] { 5, 4, 3, 2, 2, 2, 6, 7 },
                    new int[] { 6, 5, 4, 3, 2, 2, 7, 7 },
                    new int[] { 7, 6, 5, 4, 3, 2, 7, 7 },
                }
                : new int[8][]
                {
                    new int[] { 0, 0, 0, 0, 0, 0, 1, 2 },
                    new int[] { 1, 0, 0, 0, 0, 0, 2, 3 },
                    new int[] { 2, 1, 0, 0, 0, 0, 3, 4 },
                    new int[] { 3, 2, 1, 0, 0, 0, 4, 5 },
                    new int[] { 4, 3, 2, 1, 0, 0, 5, 6 },
                    new int[] { 5, 4, 3, 2, 1, 0, 6, 7 },
                    new int[] { 6, 5, 4, 3, 2, 1, 7, 7 },
                    new int[] { 7, 6, 5, 4, 3, 2, 7, 7 },
                };

            var validDatarates = new HashSet<string>()
            {
                "SF12BW125",
                "SF11BW125",
                "SF10BW125",
                "SF9BW125",
                "SF8BW125",
                "SF7BW125",
                "SF7BW250",
                "50", // FSK 50
            };

            MaxADRDataRate = 7;
            RegionLimits = new RegionLimits((min: 915, max: 928), validDatarates, validDatarates, 0, 0);
        }

        /// <summary>
        /// Calculates the frequency offset (AS923_FREQ_OFFSET_HZ) value for region AS923.
        /// </summary>
        /// <param name="frequencyChannel0">Configured frequency for radio 0.</param>
        /// <param name="frequencyChannel1">Configured frequency for radio 1.</param>
        public RegionAS923 WithFrequencyOffset(Hertz frequencyChannel0, Hertz frequencyChannel1)
        {
            FrequencyOffset = Math.Round(frequencyChannel0.Mega - Channel0Frequency, 2, MidpointRounding.AwayFromZero);

            var channel1Offset = Math.Round(frequencyChannel1.Mega - Channel1Frequency, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(channel1Offset - FrequencyOffset) > Epsilon)
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

            frequency = 0;

            if (IsValidUpstreamRxpk(upstreamChannel))
            {
                // Use the same frequency as the upstream.
                frequency = upstreamChannel.Freq;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Logic to get the correct downstream transmission frequency for region CN470.
        /// </summary>
        /// <param name="upstreamFrequency">The frequency at which the message was transmitted.</param>
        /// <param name="dataRate">The upstream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public override bool TryGetDownstreamChannelFrequency(double upstreamFrequency, ushort dataRate, out double downstreamFrequency, DeviceJoinInfo deviceJoinInfo)
        {
            downstreamFrequency = 0;

            if (IsValidUpstreamFrequencyAndDataRate(upstreamFrequency, dataRate))
            {
                // Use the same frequency as the upstream.
                downstreamFrequency = upstreamFrequency;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device.</param>
        public override RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null) =>
            new RX2ReceiveWindow(Math.Round(923.2 + FrequencyOffset, 2, MidpointRounding.AwayFromZero), 2);
    }
}
