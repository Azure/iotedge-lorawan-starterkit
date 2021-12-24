// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The 32-bit ephemeral device address assigned to a device when it joins a network. It is
    /// assigned during the join process for devices that join a network using the (preferred)
    /// over-the-air activation (OTAA) technique.
    /// </summary>
    public readonly record struct DevAddr
    {
        /* +--------|---------+
           | 31..25 |  24..0  |
           +--------+---------+
           |  NwkID | NwkAddr |
           +--------|---------+ */

        public const int Size = sizeof(uint);

        private const uint NetworkAddressMask = 0x01ff_ffff;
        private const uint NetworkIdMask = ~NetworkAddressMask;

        private readonly uint value;

        public DevAddr(uint value) => this.value = value;

        public DevAddr(int networkId, int networkAddress) :
            this(SetNetworkId(0, networkId) + SetNetworkAddress(0, networkAddress))
        { }

        /// <summary>
        /// The <c>NwkID</c> (bits 25..31).
        /// </summary>
        public int NetworkId
        {
            get => unchecked((int)((this.value & NetworkIdMask) >> 25));
            init => this.value = SetNetworkId(this.value, value);
        }

        /// <summary>
        /// Determines if <see cref="NetworkId"/> represents a private network.
        /// </summary>
        public bool IsPrivate => NetworkId is 0 or 1;

        /// <summary>
        /// The <c>NwkAddr</c> (bits 0..24).
        /// </summary>
        public int NetworkAddress
        {
            get => unchecked((int)(this.value & NetworkAddressMask));
            init => this.value = SetNetworkAddress(this.value, value);
        }

        private static uint SetNetworkId(uint devAddr, int value, [CallerArgumentExpression("value")] string? paramName = null) =>
            value is >= 0 and < 0x80
            ? (devAddr & NetworkAddressMask) | unchecked((uint)value << 25)
            : throw new ArgumentException(null, paramName);

        private static uint SetNetworkAddress(uint devAddr, int value, [CallerArgumentExpression("value")] string? paramName = null) =>
            value is >= 0 and <= (int)NetworkAddressMask
            ? (devAddr & NetworkIdMask) | unchecked((uint)value)
            : throw new ArgumentException(null, paramName);

        public override string ToString() => this.value.ToString("X8", CultureInfo.InvariantCulture);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

        /// <summary>
        /// Creates a <see cref="DevAddr"/> where its <see cref="NetworkId"/> is set to 0,
        /// representing a private network address.
        /// </summary>
        public static DevAddr Private0(int address) => new DevAddr(0, address);

        /// <summary>
        /// Creates a <see cref="DevAddr"/> where its <see cref="NetworkId"/> is set to 1,
        /// representing a private network address.
        /// </summary>
        public static DevAddr Private1(int address) => new DevAddr(1, address);

        public static DevAddr Read(Span<byte> buffer) =>
            new DevAddr(BinaryPrimitives.ReadUInt32LittleEndian(buffer));

        public static DevAddr Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out DevAddr result)
        {
            if (Hexadecimal.TryParse(input, out uint raw))
            {
                result = new DevAddr(raw);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
    }
}
