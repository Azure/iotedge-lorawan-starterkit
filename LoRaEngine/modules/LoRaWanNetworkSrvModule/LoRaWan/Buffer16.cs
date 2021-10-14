// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

    /// <summary>
    /// Represents a buffer of 16 bytes (128 bits) as a single unit.
    /// </summary>
    readonly struct Buffer16 : IEquatable<Buffer16>
    {
        public const int Size = sizeof(ulong) * 2;

        readonly ulong lo;
        readonly ulong hi;

        public Buffer16(ulong hi, ulong lo) => (this.hi, this.lo) = (hi, lo);

        public bool Equals(Buffer16 other) => this.lo == other.lo && this.hi == other.hi;
        public override bool Equals(object obj) => obj is Buffer16 other && this.Equals(other);
        public override int GetHashCode() => HashCode.Combine(this.lo, this.hi);

        public static bool operator ==(Buffer16 left, Buffer16 right) => left.Equals(right);
        public static bool operator !=(Buffer16 left, Buffer16 right) => !left.Equals(right);

        public override string ToString()
        {
            Span<char> digits = stackalloc char[32];
            Hexadecimal.Write(this.hi, digits);
            Hexadecimal.Write(this.lo, digits[16..]);
            return new string(digits);
        }

        internal static Buffer16 Parse(ReadOnlySpan<char> input) =>
            TryParse(input) is (true, var result) ? result : throw new FormatException();

        internal static (bool, Buffer16) TryParse(ReadOnlySpan<char> input) =>
            input.Length == Size * 2 && Hexadecimal.TryParse(input[..16], out var hi)
                                     && Hexadecimal.TryParse(input[16..], out var lo)
                ? (true, new Buffer16(hi, lo))
                : default;

        public static Buffer16 Read(ReadOnlySpan<byte> buffer)
        {
            var lo = BinaryPrimitives.ReadUInt64BigEndian(buffer);
            var hi = BinaryPrimitives.ReadUInt64BigEndian(buffer[8..]);
            return new Buffer16(hi, lo);
        }

        public static Buffer16 Read(ref ReadOnlySpan<byte> buffer)
        {
            var result = Read(buffer);
            buffer = buffer[Size..];
            return result;
        }

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64BigEndian(buffer, this.hi);
            BinaryPrimitives.WriteUInt64BigEndian(buffer[8..], this.lo);
            return buffer[16..];
        }
    }
}
