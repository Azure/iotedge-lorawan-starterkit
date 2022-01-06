// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    public static class RegionManager
    {
        /// <summary>
        /// Tries to get the <see cref="LoRaRegionType"/> based on <paramref name="value"/>.
        /// </summary>
        public static bool TryTranslateToRegion(LoRaRegionType value, out Region region)
        {
            region = null;
            switch (value)
            {
                case LoRaRegionType.EU863: //Case added for LoRa Basics Station compatibility
                case LoRaRegionType.EU868:
                    region = EU868;
                    return true;

                case LoRaRegionType.US902: //Case added for LoRa Basics Station compatibility
                case LoRaRegionType.US915:
                    region = US915;
                    return true;

                case LoRaRegionType.CN470RP1:
                    region = CN470RP1;
                    return true;

                case LoRaRegionType.CN470RP2:
                    region = CN470RP2;
                    return true;

                case LoRaRegionType.AS923:
                    region = AS923;
                    return true;

                case LoRaRegionType.NotSet:
                default:
                    return false;
            }
        }

        private static Region eu868;

        public static Region EU868
        {
            get
            {
                if (eu868 == null)
                {
                    eu868 = new RegionEU868();
                }

                return eu868;
            }
        }

        private static Region us915;

        public static Region US915
        {
            get
            {
                if (us915 == null)
                {
                    us915 = new RegionUS915();
                }

                return us915;
            }
        }

        private static Region cn470RP1;

        public static Region CN470RP1
        {
            get
            {
                if (cn470RP1 == null)
                {
                    cn470RP1 = new RegionCN470RP1();
                }

                return cn470RP1;
            }
        }

        private static Region cn470RP2;

        public static Region CN470RP2
        {
            get
            {
                if (cn470RP2 == null)
                {
                    cn470RP2 = new RegionCN470RP2();
                }

                return cn470RP2;
            }
        }

        public static Region AS923 => new RegionAS923();
    }
}
