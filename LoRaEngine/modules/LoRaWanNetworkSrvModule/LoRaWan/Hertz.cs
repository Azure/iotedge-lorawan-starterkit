// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Represents a frequency in Hertz.
    /// </summary>
    public readonly struct Hertz : IEquatable<Hertz>
    {
        readonly ulong value;

        public Hertz(ulong value) => this.value = value;

        public double Kilo => this.value / 1e3;
        public double Mega => this.value / 1e6;
        public double Giga => this.value / 1e9;

        public bool Equals(Hertz other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is Hertz other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => value.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(Hertz left, Hertz right) => left.Equals(right);
        public static bool operator !=(Hertz left, Hertz right) => !left.Equals(right);
    }
}
