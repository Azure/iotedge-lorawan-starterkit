// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

    /// <summary>
    /// Represents byte data of 128 bits as a single unit.
    /// </summary>
    internal readonly record struct Data128
    {
        public const int Size = sizeof(ulong) * 2;

        private readonly ulong lo;
        private readonly ulong hi;

        public Data128(ulong hi, ulong lo) => (this.hi, this.lo) = (hi, lo);

        public override string ToString()
        {
            Span<char> digits = stackalloc char[32];
#pragma warning disable IDE0058 // Expression value is never used
            Hexadecimal.Write(this.hi, digits);
            Hexadecimal.Write(this.lo, digits[16..]);
#pragma warning restore IDE0058 // Expression value is never used
            return new string(digits);
        }

        internal static Data128 Parse(ReadOnlySpan<char> input) =>
            TryParse(input) is (true, var result) ? result : throw new FormatException();

        internal static (bool, Data128) TryParse(ReadOnlySpan<char> input) =>
            input.Length == Size * 2 && Hexadecimal.TryParse(input[..16], out ulong hi)
                                     && Hexadecimal.TryParse(input[16..], out ulong lo)
                ? (true, new Data128(hi, lo))
                : default;

        public static Data128 Read(ReadOnlySpan<byte> buffer)
        {
            var hi = BinaryPrimitives.ReadUInt64BigEndian(buffer);
            var lo = BinaryPrimitives.ReadUInt64BigEndian(buffer[8..]);
            return new Data128(hi, lo);
        }

        public static Data128 Read(ref ReadOnlySpan<byte> buffer)
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
