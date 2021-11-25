// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System.Globalization;

    /// <summary>
    /// Represents a frequency in Hertz.
    /// </summary>
    public readonly record struct Hertz
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

        public override string ToString() => this.value.ToString(CultureInfo.InvariantCulture);
    }
}
