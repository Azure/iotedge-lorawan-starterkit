// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    /// <summary>
    /// A unique (usually random) 2-byte value generated by the end device and which the network
    /// server uses to keep track of each end-device.
    /// </summary>
    /// <remarks>
    /// The value is used to prevent replay attacks. If the end device sends a Join-request with a
    /// previously used DevNonce, the network server rejects the Join-request and does not allow the
    /// end device to register with the network.
    /// </remarks>
    public readonly record struct DevNonce : IComparable<DevNonce>
    {
        public const int Size = sizeof(ushort);

#pragma warning disable IDE0032 // Use auto property (explicit name)
        private readonly ushort value;

        public DevNonce(ushort value) => this.value = value;

        public ushort AsUInt16 => this.value;
#pragma warning restore IDE0032 // Use auto property

        public override string ToString() => this.value.ToString(CultureInfo.InvariantCulture);

        public int CompareTo(DevNonce other) => this.value.CompareTo(other.value);

        public static bool operator >(DevNonce left, DevNonce right) => left.CompareTo(right) > 0;
        public static bool operator <(DevNonce left, DevNonce right) => left.CompareTo(right) < 0;
        public static bool operator >=(DevNonce left, DevNonce right) => left.CompareTo(right) >= 0;
        public static bool operator <=(DevNonce left, DevNonce right) => left.CompareTo(right) <= 0;

        public static DevNonce Read(ref ReadOnlySpan<byte> buffer)
        {
            var result = Read(buffer);
            buffer = buffer[Size..];
            return result;
        }

        public static DevNonce Read(ReadOnlySpan<byte> buffer) =>
            new(BinaryPrimitives.ReadUInt16LittleEndian(buffer));

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, this.value);
            return buffer[Size..];
        }
    }
}
