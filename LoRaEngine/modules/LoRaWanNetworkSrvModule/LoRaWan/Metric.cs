// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    public static class Metric
    {
        public static Mega Mega(double value) => new Mega(value);
    }

    public readonly record struct Mega(double Value) : IFormattable
    {
        public double Units => Value * 1e6;

        public string ToString(IFormatProvider? formatProvider) => ToString(null, formatProvider);
        public string ToString(string? format) => ToString(format, null);
        public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format, formatProvider);
        public override string ToString() => ToString(null, CultureInfo.CurrentCulture);
    }
}
