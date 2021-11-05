// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using LoRaTools.LoRaPhysical;

    public static class RegionManager
    {
        public static Region CurrentRegion
        {
            get; set;
        }

        /// <summary>
        /// Tries to get the <see cref="LoRaRegionType"/> based on <paramref name="value"/>.
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

                case LoRaRegionType.CN470:
                    region = CN470;
                    return true;

                case LoRaRegionType.AS923:
                    region = AS923;
                    return true;

                case LoRaRegionType.NotSet:
                default:
                    return false;
            }
        }

        /// <summary>
        /// Tries the resolve region.
        /// </summary>
        /// <returns><c>true</c>, if a region was resolved, <c>false</c> otherwise.</returns>
        /// <param name="rxpk">Rxpk.</param>
        /// <param name="region">Region.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        public static bool TryResolveRegion(Rxpk rxpk, out Region region)
        {
            if (rxpk is null) throw new ArgumentNullException(nameof(rxpk));

            region = null;

            // EU863-870
            if (rxpk.Freq is < 870 and > 863)
            {
                region = EU868;
                return true;
            }// US902-928 frequency band, upstream messages are between 902 and 915.
            else if (rxpk.Freq is <= 915 and >= 902)
            {
                region = US915;
                return true;
            }
            else if (rxpk.Freq is <= 510 and >= 470)
            {
                region = CN470;
                return true;
            }
            else if (rxpk.Freq is <= 928 and > 915)
            {
                region = AS923;
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
#pragma warning disable CA1508 // Avoid dead conditional code
                    // False positive
                    if (eu868 == null)
#pragma warning restore CA1508 // Avoid dead conditional code
                    {
                        eu868 = new RegionEU868();
                    }
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
#pragma warning disable CA1508 // Avoid dead conditional code
                    // False positive
                    if (us915 == null)
#pragma warning restore CA1508 // Avoid dead conditional code
                    {
                        us915 = new RegionUS915();
                    }
                }

                return us915;
            }
        }

        private static Region cn470;

        public static Region CN470
        {
            get
            {
                if (cn470 == null)
                {
#pragma warning disable CA1508 // Avoid dead conditional code
                    // False positive
                    if (cn470 == null)
#pragma warning restore CA1508 // Avoid dead conditional code
                    {
                        cn470 = new RegionCN470();
                    }
                }

                return cn470;
            }
        }

        private static Region as923;

        public static Region AS923
        {
            get
            {
                if (as923 == null)
                {
#pragma warning disable CA1508 // Avoid dead conditional code
                    // False positive
                    if (as923 == null)
#pragma warning restore CA1508 // Avoid dead conditional code
                    {
                        as923 = new RegionAS923(new LoRaWan.Hertz(923200000), new LoRaWan.Hertz(923400000));
                    }
                }

                return as923;
            }
        }
    }
}
