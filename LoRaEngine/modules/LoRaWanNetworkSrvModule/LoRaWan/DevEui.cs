// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    /// <summary>
    /// Global end-device ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space
    /// that uniquely identifies the end-device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For OTAA devices, the DevEUI MUST be stored in the end-device before the Join procedure
    /// is executed. ABP devices do not need the DevEUI to be stored in the device itself, but
    /// it is recommended to do so.</para>
    /// <para>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.</para>
    /// </remarks>
    public readonly struct DevEui : IEquatable<DevEui>
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public DevEui(ulong value) => this.value = value;

        public bool Equals(DevEui other) => this.value == other.value;
        public override bool Equals(object obj) => obj is DevEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString("X16", CultureInfo.InvariantCulture);

        public static bool operator ==(DevEui left, DevEui right) => left.Equals(right);
        public static bool operator !=(DevEui left, DevEui right) => !left.Equals(right);

        public static DevEui Read(ReadOnlySpan<byte> buffer) =>
            new(BinaryPrimitives.ReadUInt64LittleEndian(buffer));

        public static DevEui Read(ref ReadOnlySpan<byte> buffer)
        {
            var result = Read(buffer);
            buffer = buffer[Size..];
            return result;
        }

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

        public static DevEui Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out DevEui result)
        {
            if (Hexadecimal.TryParse(input, out var raw, '-'))
            {
                result = new DevEui(raw);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
    }
}
