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
    using static LoRaWan.DataRate;
    using static LoRaWan.Metric;

    public class RegionAS923 : Region
    {
        private static readonly Hertz Channel0Frequency = Mega(923.2);
        private static readonly Hertz Channel1Frequency = Mega(923.4);

        private readonly bool useDwellTimeLimit;

        public long FrequencyOffset { get; private set; }

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
            DRtoConfiguration.Add(DR0, (configuration: "SF12BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(DR1, (configuration: "SF11BW125", maxPyldSize: 59));
            DRtoConfiguration.Add(DR2, (configuration: "SF10BW125", maxPyldSize: 123));
            DRtoConfiguration.Add(DR3, (configuration: "SF9BW125", maxPyldSize: 123));
            DRtoConfiguration.Add(DR4, (configuration: "SF8BW125", maxPyldSize: 230));
            DRtoConfiguration.Add(DR5, (configuration: "SF7BW125", maxPyldSize: 230));
            DRtoConfiguration.Add(DR6, (configuration: "SF7BW250", maxPyldSize: 230));
            DRtoConfiguration.Add(DR7, (configuration: "50", maxPyldSize: 230)); // FSK 50

            if (useDwellTimeLimit)
            {
                DRtoConfiguration[DR2] = (configuration: "SF10BW125", maxPyldSize: 19);
                DRtoConfiguration[DR3] = (configuration: "SF10BW125", maxPyldSize: 61);
                DRtoConfiguration[DR4] = (configuration: "SF10BW125", maxPyldSize: 133);
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
                ? new[]
                {
                    new[] { DR2, DR2, DR2, DR2, DR2, DR2, DR2, DR2 },
                    new[] { DR2, DR2, DR2, DR2, DR2, DR2, DR2, DR3 },
                    new[] { DR2, DR2, DR2, DR2, DR2, DR2, DR3, DR4 },
                    new[] { DR3, DR2, DR2, DR2, DR2, DR2, DR4, DR5 },
                    new[] { DR4, DR3, DR2, DR2, DR2, DR2, DR5, DR6 },
                    new[] { DR5, DR4, DR3, DR2, DR2, DR2, DR6, DR7 },
                    new[] { DR6, DR5, DR4, DR3, DR2, DR2, DR7, DR7 },
                    new[] { DR7, DR6, DR5, DR4, DR3, DR2, DR7, DR7 },
                }
                : new[]
                {
                    new[] { DR0, DR0, DR0, DR0, DR0, DR0, DR1, DR2 },
                    new[] { DR1, DR0, DR0, DR0, DR0, DR0, DR2, DR3 },
                    new[] { DR2, DR1, DR0, DR0, DR0, DR0, DR3, DR4 },
                    new[] { DR3, DR2, DR1, DR0, DR0, DR0, DR4, DR5 },
                    new[] { DR4, DR3, DR2, DR1, DR0, DR0, DR5, DR6 },
                    new[] { DR5, DR4, DR3, DR2, DR1, DR0, DR6, DR7 },
                    new[] { DR6, DR5, DR4, DR3, DR2, DR1, DR7, DR7 },
                    new[] { DR7, DR6, DR5, DR4, DR3, DR2, DR7, DR7 },
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

            MaxADRDataRate = DR7;
            RegionLimits = new RegionLimits((Min: Mega(915), Max: Mega(928)), validDatarates, validDatarates, 0, 0);
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
        public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, DataRate? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = null)
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
    }
}
