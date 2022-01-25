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
            if (!IsSesquiWord(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            this.value = value;
        }

        /// <summary>
        /// Gets the network identifier (NwkID), the 7 LBS.
        /// </summary>
        public int NetworkId => this.value & 0b111_1111;

        public override string ToString() => this.value.ToString("X6", CultureInfo.InvariantCulture);

        public static NetId Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out NetId result)
        {
            if (int.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw) && IsSesquiWord(raw))
            {
                result = new NetId(raw);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        private static bool IsSesquiWord(int n) => unchecked((uint)n & 0xff00_0000) == 0;

        public Span<byte> Write(Span<byte> buffer)
        {
            unchecked
            {
                buffer[0] = (byte)this.value;
                buffer[1] = (byte)(this.value >> 8);
                buffer[2] = (byte)(this.value >> 16);
            }
            return buffer[Size..];
        }

        public static NetId Read(ReadOnlySpan<byte> buffer) => new(unchecked((int)LittleEndianReader.ReadUInt24(buffer)));
    }
}
