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

        public UInt128(ulong lo, ulong hi) => (this.hi, this.lo) = (hi, lo);

        public bool Equals(UInt128 other) => this.lo == other.lo && this.hi == other.hi;
        public override bool Equals(object obj) => obj is UInt128 other && this.Equals(other);
        public override int GetHashCode() => HashCode.Combine(this.lo, this.hi);

        public static bool operator ==(UInt128 left, UInt128 right) => left.Equals(right);
        public static bool operator !=(UInt128 left, UInt128 right) => !left.Equals(right);

        public override string ToString()
        {
            Span<char> digits = stackalloc char[32];
            Hexadecimal.Write(this.lo, digits[..16]);
            Hexadecimal.Write(this.hi, digits[16..]);
            return new string(digits);
        }

        public byte[] GetBytes()
        {
            var bytes = new byte[Size];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(), this.lo);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), this.hi);
            return bytes;
        }
    }
}
