// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Runtime.InteropServices;
    using System.Text;

    internal ref struct ByteSpanReader
    {
        private ReadOnlySpan<byte> span;

        public ByteSpanReader(ReadOnlySpan<byte> span) => this.span = span;

        private T Read<T>(int size, T result)
        {
            this.span = this.span[size..];
            return result;
        }

        private T Read<T>(T result) where T : unmanaged => Read(Marshal.SizeOf<T>(), result);

        public byte[] Read(int size) => Read(size, this.span[..size].ToArray());
        public byte Read() => Read(this.span[0]);
        public byte[] ReadAll() => Read(this.span.Length);
        public string ReadUtf8String(int size) => ReadString(size, Encoding.UTF8);
        public string ReadString(int size, Encoding encoding) => Read(size, encoding.GetString(this.span[..size]));
        public ushort ReadUInt16LittleEndian() => Read(BinaryPrimitives.ReadUInt16LittleEndian(this.span));
        public uint ReadUInt32LittleEndian() => Read(BinaryPrimitives.ReadUInt32LittleEndian(this.span));
    }
}
