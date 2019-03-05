// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System.Collections.Generic;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public static class RegionFactory
    {
        public static Region CurrentRegion
        {
            get; set;
        }

        public static bool TryResolveRegion(Rxpk rxpk)
        {
            // EU863-870
            if (rxpk.Freq < 870 && rxpk.Freq > 863)
            {
                CurrentRegion = CreateEU868Region();
                return true;
            }// US902-928
            else if (rxpk.Freq <= 928 && rxpk.Freq >= 902)
            {
                CurrentRegion = CreateUS915Region();
                return true;
            }
            else
            {
                Logger.Log("RegionFactory", "The current frequency plan is not supported. Currently only EU868 and US915 frequency bands are supported.", LogLevel.Error);
                return false;
            }
        }

        public static Region CreateEU868Region()
        {
            Region r = new Region(
                LoRaRegion.EU868,
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
            r.DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 59));
            r.DRtoConfiguration.Add(1, (configuration: "SF11BW125", maxPyldSize: 59));
            r.DRtoConfiguration.Add(2, (configuration: "SF10BW125", maxPyldSize: 59));
            r.DRtoConfiguration.Add(3, (configuration: "SF9BW125", maxPyldSize: 123));
            r.DRtoConfiguration.Add(4, (configuration: "SF8BW125", maxPyldSize: 230));
            r.DRtoConfiguration.Add(5, (configuration: "SF7BW125", maxPyldSize: 230));
            r.DRtoConfiguration.Add(6, (configuration: "SF7BW250", maxPyldSize: 230));
            r.DRtoConfiguration.Add(7, (configuration: "50", maxPyldSize: 230)); // USED FOR GFSK

            r.TXPowertoMaxEIRP.Add(0, 16);
            r.TXPowertoMaxEIRP.Add(1, 14);
            r.TXPowertoMaxEIRP.Add(2, 12);
            r.TXPowertoMaxEIRP.Add(3, 10);
            r.TXPowertoMaxEIRP.Add(4, 8);
            r.TXPowertoMaxEIRP.Add(5, 6);
            r.TXPowertoMaxEIRP.Add(6, 4);
            r.TXPowertoMaxEIRP.Add(7, 2);

            r.RX1DROffsetTable = new int[8, 6]
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
            r.MaxADRDataRate = 5;

            List<string> euValidDataranges = new List<string>()
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

            r.RegionLimits = new RegionLimits((min: 863, max: 870), euValidDataranges);
            return r;
        }

        public static Region CreateUS915Region()
        {
            Region r = new Region(
                LoRaRegion.US915,
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
            r.DRtoConfiguration.Add(0, (configuration: "SF10BW125", maxPyldSize: 19));
            r.DRtoConfiguration.Add(1, (configuration: "SF9BW125", maxPyldSize: 61));
            r.DRtoConfiguration.Add(2, (configuration: "SF8BW125", maxPyldSize: 133));
            r.DRtoConfiguration.Add(3, (configuration: "SF7BW125", maxPyldSize: 250));
            r.DRtoConfiguration.Add(4, (configuration: "SF8BW500", maxPyldSize: 250));
            r.DRtoConfiguration.Add(8, (configuration: "SF12BW500", maxPyldSize: 61));
            r.DRtoConfiguration.Add(9, (configuration: "SF11BW500", maxPyldSize: 137));
            r.DRtoConfiguration.Add(10, (configuration: "SF10BW500", maxPyldSize: 250));
            r.DRtoConfiguration.Add(11, (configuration: "SF9BW500", maxPyldSize: 250));
            r.DRtoConfiguration.Add(12, (configuration: "SF8BW500", maxPyldSize: 250));
            r.DRtoConfiguration.Add(13, (configuration: "SF7BW500", maxPyldSize: 250));

            for (uint i = 0; i < 14; i++)
            {
                r.TXPowertoMaxEIRP.Add(i, 30 - i);
            }

            r.RX1DROffsetTable = new int[5, 4]
            {
            { 10, 9, 8, 8 },
            { 11, 10, 9, 8 },
            { 12, 11, 10, 9 },
            { 13, 12, 11, 10 },
            { 13, 13, 12, 11 },
            };

            r.MaxADRDataRate = 3;
            List<string> usValidDataranges = new List<string>()
            {
                "SF10BW125", // 0
                "SF9BW125", // 1
                "SF8BW125", // 2
                "SF7BW125", // 3
                "SF8BW500", // 4
                "SF12BW500", // 8
                "SF11BW500", // 9
                "SF10BW500", // 10
                "SF9BW500", // 11
                "SF8BW500", // 12
                "SF8BW500" // 13
            };

            r.RegionLimits = new RegionLimits((min: 902.3, max: 927.5), usValidDataranges);
            return r;
        }
    }
}
