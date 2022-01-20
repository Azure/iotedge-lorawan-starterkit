// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Text;


    internal static class SpanExtensions
    {
        public static ByteSpanReader GetReader(this byte[] source) => new(source);
        public static ByteSpanReader GetReader(this Span<byte> source) => new(source);
        public static ByteSpanReader GetReader(this ReadOnlySpan<byte> source) => new(source);

        public static Span<T> Write<T>(this Span<T> span, T value)
        {
            span[0] = value;
            return span[1..];
        }

        public static Span<byte> WriteUtf8(this Span<byte> destination, ReadOnlySpan<char> chars) =>
            destination.Write(chars, Encoding.UTF8);

        public static Span<byte> Write(this Span<byte> destination, ReadOnlySpan<char> chars, Encoding encoding)
        {
            var count = encoding.GetBytes(chars, destination);
            return destination[count..];
        }

        public static Span<T> Write<T>(this Span<T> destination, ReadOnlySpan<T> source)
        {
            source.CopyTo(destination);
            return destination[source.Length..];
        }

        public static Span<byte> WriteUInt16LittleEndian(this Span<byte> span, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span, value);
            return span[sizeof(ushort)..];
        }

        public static Span<byte> WriteUInt32LittleEndian(this Span<byte> span, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span, value);
            return span[sizeof(uint)..];
        }
    }
}
