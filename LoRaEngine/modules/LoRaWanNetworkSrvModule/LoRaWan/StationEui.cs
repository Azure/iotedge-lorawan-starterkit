namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    /// <summary>
    /// ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space that uniquely identifies
    /// a station.
    /// </summary>
    public readonly struct StationEui : IEquatable<StationEui>
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public StationEui(ulong value) => this.value = value;

        public bool Equals(StationEui other) => this.value == other.value;
        public override bool Equals(object obj) => obj is StationEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString("X16", CultureInfo.InvariantCulture);

        public static bool operator ==(StationEui left, StationEui right) => left.Equals(right);
        public static bool operator !=(StationEui left, StationEui right) => !left.Equals(right);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, this.value);
            return buffer[Size..];
        }
    }
}
