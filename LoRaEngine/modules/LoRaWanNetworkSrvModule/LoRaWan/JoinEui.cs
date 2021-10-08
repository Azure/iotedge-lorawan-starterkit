// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    /// <summary>
    /// Global application ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space
    /// that uniquely identifies the Join Server that is able to assist in the processing of
    /// the Join procedure and the session keys derivation.
    /// </summary>
    /// <remarks>
    /// For OTAA devices, the JoinEUI MUST be stored in the end-device before the Join procedure
    /// is executed. The JoinEUI is not required for ABP only end-devices.
    /// </remarks>
    public readonly struct JoinEui : IEquatable<JoinEui>
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public JoinEui(ulong value) => this.value = value;

        public bool Equals(JoinEui other) => this.value == other.value;
        public override bool Equals(object obj) => obj is JoinEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString("X16", CultureInfo.InvariantCulture);

        public static bool operator ==(JoinEui left, JoinEui right) => left.Equals(right);
        public static bool operator !=(JoinEui left, JoinEui right) => !left.Equals(right);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, this.value);
            return buffer[Size..];
        }
    }
}
