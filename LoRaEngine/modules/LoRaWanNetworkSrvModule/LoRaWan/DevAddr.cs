// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// The 32-bit ephemeral device address assigned to a device when it joins a network. It is
    /// assigned during the join process for devices that join a network using the (preferred)
    /// over-the-air activation (OTAA) technique.
    /// </summary>
    public readonly struct DevAddr : IEquatable<DevAddr>
    {
        /* +--------|---------+
           | 31..25 |  24..0  |
           +--------+---------+
           |  NwkID | NwkAddr |
           +--------|---------+ */

        public const int Size = sizeof(uint);

        const uint NetworkAddressMask = 0x01ff_ffff;

        readonly uint value;

        public DevAddr(uint value) => this.value = value;

        public uint AsUInt32 => this.value;

        /// <summary>
        /// The <c>NwkID</c> (bits 25..31).
        /// </summary>
        public int NetworkId => unchecked((int)((this.value & ~NetworkAddressMask) >> 25));

        /// <summary>
        /// The <c>NwkAddr</c> (bits 0..24).
        /// </summary>
        public int NetworkAddress => unchecked((int)(this.value & NetworkAddressMask));

        public bool Equals(DevAddr other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is DevAddr other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString("X8", CultureInfo.InvariantCulture);

        public static bool operator ==(DevAddr left, DevAddr right) => left.Equals(right);
        public static bool operator !=(DevAddr left, DevAddr right) => !left.Equals(right);
    }
}
