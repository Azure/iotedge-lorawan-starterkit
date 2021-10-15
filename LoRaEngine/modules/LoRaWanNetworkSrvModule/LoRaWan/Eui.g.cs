// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//------------------------------------------------------------------------------
// This code was generated by a tool.
// Changes to this file will be lost if the code is re-generated.
//------------------------------------------------------------------------------

#nullable enable

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

    readonly partial struct DevEui : IEquatable<DevEui>
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public DevEui(ulong value) => this.value = value;

        public bool Equals(DevEui other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is DevEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString()
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            Span<char> chars = stackalloc char[bytes.Length * 3 - 1];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, this.value);
            Hexadecimal.Write(bytes, chars, '-');
            return new string(chars);
        }

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

    readonly partial struct JoinEui : IEquatable<JoinEui>
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public JoinEui(ulong value) => this.value = value;

        public bool Equals(JoinEui other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is JoinEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString()
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            Span<char> chars = stackalloc char[bytes.Length * 3 - 1];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, this.value);
            Hexadecimal.Write(bytes, chars, '-');
            return new string(chars);
        }

        public static bool operator ==(JoinEui left, JoinEui right) => left.Equals(right);
        public static bool operator !=(JoinEui left, JoinEui right) => !left.Equals(right);

        public static JoinEui Read(ReadOnlySpan<byte> buffer) =>
            new(BinaryPrimitives.ReadUInt64LittleEndian(buffer));

        public static JoinEui Read(ref ReadOnlySpan<byte> buffer)
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

        public static JoinEui Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out JoinEui result)
        {
            if (Hexadecimal.TryParse(input, out var raw, '-'))
            {
                result = new JoinEui(raw);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
    }

    readonly partial struct StationEui : IEquatable<StationEui>
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public StationEui(ulong value) => this.value = value;

        public bool Equals(StationEui other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is StationEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString()
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            Span<char> chars = stackalloc char[bytes.Length * 3 - 1];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, this.value);
            Hexadecimal.Write(bytes, chars, '-');
            return new string(chars);
        }

        public static bool operator ==(StationEui left, StationEui right) => left.Equals(right);
        public static bool operator !=(StationEui left, StationEui right) => !left.Equals(right);

        public static StationEui Read(ReadOnlySpan<byte> buffer) =>
            new(BinaryPrimitives.ReadUInt64LittleEndian(buffer));

        public static StationEui Read(ref ReadOnlySpan<byte> buffer)
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

        public static StationEui Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out StationEui result)
        {
            if (Hexadecimal.TryParse(input, out var raw, '-'))
            {
                result = new StationEui(raw);
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
