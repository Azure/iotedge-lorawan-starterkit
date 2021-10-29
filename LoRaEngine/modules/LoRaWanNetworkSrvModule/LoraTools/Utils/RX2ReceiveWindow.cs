namespace LoRaTools.Utils
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    public struct RX2ReceiveWindow : IEquatable<RX2ReceiveWindow>
    {
        public RX2ReceiveWindow(double frequency, ushort dataRate)
        {
            Frequency = frequency;
            DataRate = dataRate;
        }

        public double Frequency { get; }
        public ushort DataRate { get; }

        public bool Equals([AllowNull] RX2ReceiveWindow other) => Frequency == other.Frequency && DataRate == other.DataRate;
        public override bool Equals(object obj) => obj is RX2ReceiveWindow other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Frequency, DataRate);

        public static bool operator ==(RX2ReceiveWindow left, RX2ReceiveWindow right) => left.Equals(right);
        public static bool operator !=(RX2ReceiveWindow left, RX2ReceiveWindow right) => !left.Equals(right);
    }
}
