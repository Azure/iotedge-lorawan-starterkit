// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;

    internal readonly ref struct SpanWriter<T>
    {
        public SpanWriter(Span<T> span) : this(span, span) { }

        private SpanWriter(ReadOnlySpan<T> fullSpan, Span<T> span)
        {
            FullSpan = fullSpan;
            Span = span;
        }

        public ReadOnlySpan<T> FullSpan { get; }
        public Span<T> Span { get; }
        public int Length => FullSpan.Length - Span.Length;

        public SpanWriter<T> Write(T input)
        {
            Span[0] = input;
            return Skip(1);
        }

        public SpanWriter<T> Write(ReadOnlySpan<T> input)
        {
            input.CopyTo(Span);
            return Skip(input.Length);
        }

        public SpanWriter<T> Skip(int count) => new(FullSpan, Span[count..]);
    }

    internal static class SpanWriterExtensions
    {
        public static SpanWriter<T> GetWriter<T>(this Span<T> span) => new(span);

        public static SpanWriter<byte> WriteUInt16LittleEndian(this SpanWriter<byte> writer, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(writer.Span, value);
            return writer.Skip(sizeof(ushort));
        }

        public static SpanWriter<byte> WriteUInt32LittleEndian(this SpanWriter<byte> writer, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(writer.Span, value);
            return writer.Skip(sizeof(uint));
        }
    }
}
