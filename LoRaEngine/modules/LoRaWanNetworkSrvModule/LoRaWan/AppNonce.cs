// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A random value or some form of unique ID provided by the network server and used by the
    /// end-device to derive the two session keys <c>NwkSKey</c> and <c>AppSKey</c>.
    /// </summary>
    public readonly record struct AppNonce
    {
        public const int Size = 3;
        public const int MaxValue = 0xff_ffff;

        private readonly int value;

        public AppNonce(int value) =>
            this.value = value is >= 0 and <= MaxValue ? value : throw new ArgumentOutOfRangeException(nameof(value), value, null);

        public override string ToString() => this.value.ToString(CultureInfo.InvariantCulture);

        public static explicit operator int(AppNonce nonce) => nonce.value;

        public Span<byte> Write(Span<byte> buffer)
        {
            if (buffer.Length < Size) throw new ArgumentException("Insufficient buffer length.");

            unchecked
            {
                buffer[0] = (byte)this.value;
                buffer[1] = (byte)(this.value >> 8);
                buffer[2] = (byte)(this.value >> 16);
            }
            return buffer[Size..];
        }

        public static AppNonce Read(ReadOnlySpan<byte> buffer) => new(unchecked((int)LittleEndianReader.ReadUInt24(buffer)));
    }
}
