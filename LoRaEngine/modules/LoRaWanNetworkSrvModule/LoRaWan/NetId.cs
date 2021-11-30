// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A NetID that is a 24-bit value used for identifying LoRaWAN networks. It is assigned by the
    /// LoRa Alliance. It is used by networks for assigning network-specific addresses to their
    /// end-devices (i.e., DevAddr) so that uplink frames sent by those devices even when they are
    /// roaming outside their home network can be forwarded to their home network.
    /// </summary>
    public readonly record struct NetId
    {
        public const int Size = 3;

        private readonly int value; // 24-bit

        public NetId(int value)
        {
            if (unchecked((uint)value & 0xffff_0000_0000_0000) != 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            this.value = value;
        }

        /// <summary>
        /// Gets the network identifier (NwkID), the 7 LBS.
        /// </summary>
        public int NetworkId => this.value & 0b111_1111;

        public override string ToString() => this.value.ToString("X6", CultureInfo.InvariantCulture);
    }
}
