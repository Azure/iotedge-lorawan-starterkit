// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Data;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Org.BouncyCastle.Utilities.Collections;

    public static class RegionManager
    {
        private static readonly object RegionLock = new object();

        public static Region CurrentRegion
        {
            get; set;
        }

        /// <summary>
        /// Tries to get the <see cref="LoRaRegionType"/> based on <paramref name="value"/>
        /// </summary>
        public static bool TryTranslateToRegion(LoRaRegionType value, out Region region)
        {
            region = null;
            switch (value)
            {
                case LoRaRegionType.EU868:
                    region = EU868;
                    return true;

                case LoRaRegionType.US915:
                    region = US915;
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Tries the resolve region.
        /// </summary>
        /// <returns><c>true</c>, if a region was resolved, <c>false</c> otherwise.</returns>
        /// <param name="rxpk">Rxpk.</param>
        /// <param name="region">Region.</param>
        public static bool TryResolveRegion(Rxpk rxpk, out Region region)
        {
            region = null;

            // EU863-870
            if (rxpk.Freq < 870 && rxpk.Freq > 863)
            {
                region = EU868;
                return true;
            }// US902-928
            else if (rxpk.Freq <= 928 && rxpk.Freq >= 902)
            {
                region = US915;
                return true;
            }

            return false;
        }

        private static Region eu868;

        public static Region EU868
        {
            get
            {
                if (eu868 == null)
                {
                    lock (RegionLock)
                    {
                        if (eu868 == null)
                        {
                            CreateEU868Region();
                        }
                    }
                }

                return eu868;
            }
        }

        private static void CreateEU868Region()
        {
            eu868 = new Region(
                LoRaRegionType.EU868,
                0x34,
                ConversionHelper.StringToByteArray("C194C1"),
                (frequency: 869.525, datr: 0),
                1,
                2,
                5,
                6,
                16384,
                64,
                32,
                (min: 1, max: 3));
            eu868.DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 59));
            eu868.DRtoConfiguration.Add(1, (configuration: "SF11BW125", maxPyldSize: 59));
            eu868.DRtoConfiguration.Add(2, (configuration: "SF10BW125", maxPyldSize: 59));
            eu868.DRtoConfiguration.Add(3, (configuration: "SF9BW125", maxPyldSize: 123));
            eu868.DRtoConfiguration.Add(4, (configuration: "SF8BW125", maxPyldSize: 230));
            eu868.DRtoConfiguration.Add(5, (configuration: "SF7BW125", maxPyldSize: 230));
            eu868.DRtoConfiguration.Add(6, (configuration: "SF7BW250", maxPyldSize: 230));
            eu868.DRtoConfiguration.Add(7, (configuration: "50", maxPyldSize: 230)); // USED FOR GFSK

            eu868.TXPowertoMaxEIRP.Add(0, 16);
            eu868.TXPowertoMaxEIRP.Add(1, 14);
            eu868.TXPowertoMaxEIRP.Add(2, 12);
            eu868.TXPowertoMaxEIRP.Add(3, 10);
            eu868.TXPowertoMaxEIRP.Add(4, 8);
            eu868.TXPowertoMaxEIRP.Add(5, 6);
            eu868.TXPowertoMaxEIRP.Add(6, 4);
            eu868.TXPowertoMaxEIRP.Add(7, 2);

            eu868.RX1DROffsetTable = new int[8, 6]
            {
            { 0, 0, 0, 0, 0, 0 },
            { 1, 0, 0, 0, 0, 0 },
            { 2, 1, 0, 0, 0, 0 },
            { 3, 2, 1, 0, 0, 0 },
            { 4, 3, 2, 1, 0, 0 },
            { 5, 4, 3, 2, 1, 0 },
            { 6, 5, 4, 3, 2, 1 },
            { 7, 6, 5, 4, 3, 2 }
            };
            HashSet<string> validDataRangeUpAndDownstream = new HashSet<string>()
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

            eu868.MaxADRDataRate = 5;
            eu868.RegionLimits = new RegionLimits((min: 863, max: 870), validDataRangeUpAndDownstream, validDataRangeUpAndDownstream, 0, 0);
        }

        private static Region us915;

        public static Region US915
        {
            get
            {
                if (us915 == null)
                {
                    lock (RegionLock)
                    {
                        if (us915 == null)
                        {
                            CreateUS915Region();
                        }
                    }
                }

                return us915;
            }
        }

        private static void CreateUS915Region()
        {
            us915 = new Region(
                LoRaRegionType.US915,
                0x34,
                null, // no GFSK in US Band
                (frequency: 923.3, datr: 8),
                1,
                2,
                5,
                6,
                16384,
                64,
                32,
                (min: 1, max: 3));
            us915.DRtoConfiguration.Add(0, (configuration: "SF10BW125", maxPyldSize: 19));
            us915.DRtoConfiguration.Add(1, (configuration: "SF9BW125", maxPyldSize: 61));
            us915.DRtoConfiguration.Add(2, (configuration: "SF8BW125", maxPyldSize: 133));
            us915.DRtoConfiguration.Add(3, (configuration: "SF7BW125", maxPyldSize: 250));
            us915.DRtoConfiguration.Add(4, (configuration: "SF8BW500", maxPyldSize: 250));
            us915.DRtoConfiguration.Add(8, (configuration: "SF12BW500", maxPyldSize: 61));
            us915.DRtoConfiguration.Add(9, (configuration: "SF11BW500", maxPyldSize: 137));
            us915.DRtoConfiguration.Add(10, (configuration: "SF10BW500", maxPyldSize: 250));
            us915.DRtoConfiguration.Add(11, (configuration: "SF9BW500", maxPyldSize: 250));
            us915.DRtoConfiguration.Add(12, (configuration: "SF8BW500", maxPyldSize: 250));
            us915.DRtoConfiguration.Add(13, (configuration: "SF7BW500", maxPyldSize: 250));

            for (uint i = 0; i < 14; i++)
            {
                us915.TXPowertoMaxEIRP.Add(i, 30 - i);
            }

            us915.RX1DROffsetTable = new int[5, 4]
            {
            { 10, 9, 8, 8 },
            { 11, 10, 9, 8 },
            { 12, 11, 10, 9 },
            { 13, 12, 11, 10 },
            { 13, 13, 12, 11 },
            };

            HashSet<string> upstreamValidDataranges = new HashSet<string>()
            {
                "SF10BW125", // 0
                "SF9BW125", // 1
                "SF8BW125", // 2
                "SF7BW125", // 3
                "SF8BW500", // 4
            };

            HashSet<string> downstreamValidDataranges = new HashSet<string>()
            {
                "SF12BW500", // 8
                "SF11BW500", // 9
                "SF10BW500", // 10
                "SF9BW500", // 11
                "SF8BW500", // 12
                "SF7BW500" // 13
            };

            us915.MaxADRDataRate = 3;
            us915.RegionLimits = new RegionLimits((min: 902.3, max: 927.5), upstreamValidDataranges, downstreamValidDataranges, 0, 8);
        }
    }
}
