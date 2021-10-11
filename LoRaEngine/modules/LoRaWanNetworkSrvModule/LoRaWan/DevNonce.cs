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
    public readonly struct DevNonce : IEquatable<DevNonce>
    {
        public const int Size = sizeof(ushort);

        readonly ushort value;

        public DevNonce(ushort value) => this.value = value;

        public ushort AsUInt16 => this.value;

        public bool Equals(DevNonce other) => this.value == other.value;
        public override bool Equals(object obj) => obj is DevNonce other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => value.ToString("X4", CultureInfo.InvariantCulture);

        public static bool operator ==(DevNonce left, DevNonce right) => left.Equals(right);
        public static bool operator !=(DevNonce left, DevNonce right) => !left.Equals(right);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, this.value);
            return buffer[Size..];
        }
    }
}
