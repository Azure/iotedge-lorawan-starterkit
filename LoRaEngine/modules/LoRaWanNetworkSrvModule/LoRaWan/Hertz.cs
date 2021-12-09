// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Represents a frequency in Hertz.
    /// </summary>
    public readonly record struct Hertz : IComparable<Hertz>
    {
#pragma warning disable IDE0032 // Use auto property
        private readonly ulong value;
#pragma warning restore IDE0032 // Use auto property

        public Hertz(ulong value) => this.value = value;

#pragma warning disable IDE0032 // Use auto property
        public ulong AsUInt64 => this.value;
#pragma warning restore IDE0032 // Use auto property

        public double Kilo => this.value / 1e3;
        public double Mega => this.value / 1e6;
        public double Giga => this.value / 1e9;

        public static Hertz FromMega(double value) => new Hertz(checked((ulong)(value * 1e6)));

        public static implicit operator Hertz(Mega value) => new Hertz(checked((ulong)value.Unit));

        public override string ToString() => this.value.ToString(CultureInfo.InvariantCulture);

        public static explicit operator ulong(Hertz value) => value.value;

        public int CompareTo(Hertz other) => this.value.CompareTo(other.value);

        public static bool operator <(Hertz a, Hertz b) => a.CompareTo(b) < 0;
        public static bool operator <=(Hertz a, Hertz b) => a.CompareTo(b) <= 0;
        public static bool operator >(Hertz a, Hertz b) => a.CompareTo(b) > 0;
        public static bool operator >=(Hertz a, Hertz b) => a.CompareTo(b) >= 0;

        public static Hertz operator +(Hertz a, long offset) => new(checked((ulong)((long)a.value + offset)));
        public static Hertz operator +(Hertz a, Mega offset) => new(checked((ulong)((long)a.value + offset.Unit)));
        public static long operator -(Hertz a, Hertz b) => checked((long)a.value - (long)b.value);
    }
}
