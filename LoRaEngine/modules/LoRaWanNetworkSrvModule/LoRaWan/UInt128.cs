// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

    public readonly struct UInt128 : IEquatable<UInt128>
    {
        public const int Size = sizeof(ulong) * 2;

        readonly ulong lo;
        readonly ulong hi;

        public UInt128(ulong hi, ulong lo) => (this.hi, this.lo) = (hi, lo);

        public bool Equals(UInt128 other) => this.lo == other.lo && this.hi == other.hi;
        public override bool Equals(object obj) => obj is UInt128 other && this.Equals(other);
        public override int GetHashCode() => HashCode.Combine(this.lo, this.hi);

        public static bool operator ==(UInt128 left, UInt128 right) => left.Equals(right);
        public static bool operator !=(UInt128 left, UInt128 right) => !left.Equals(right);

        public override string ToString()
        {
            Span<char> digits = stackalloc char[32];
            Hexadecimal.Write(this.hi, digits);
            Hexadecimal.Write(this.lo, digits[16..]);
            return new string(digits);
        }

        internal static UInt128 Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        internal static bool TryParse(ReadOnlySpan<char> input, out UInt128 result)
        {
            if (input.Length == Size * 2
                && Hexadecimal.TryParse(input[..16], out var hi)
                && Hexadecimal.TryParse(input[16..], out var lo))
            {
                result = new UInt128(hi, lo);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        public Span<byte> WriteLittleEndian(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, this.lo);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer[8..], this.hi);
            return buffer[16..];
        }

        public Span<byte> WriteBigEndian(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64BigEndian(buffer, this.hi);
            BinaryPrimitives.WriteUInt64BigEndian(buffer[8..], this.lo);
            return buffer[16..];
        }
    }
}
