// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// An integer value between 0 and 15 that influences modulation (LoRa or FSK), the spreading
    /// factor (SF) and the bandwidth (BW).
    /// </summary>
    public readonly struct DataRate : IEquatable<DataRate>
    {
        readonly byte value;

        public DataRate(int value) =>
            this.value = value is var v and >= 0 and <= 15
                ? unchecked((byte)v)
                : throw new ArgumentOutOfRangeException(nameof(value), value, null);

        public bool Equals(DataRate other) => this.value == other.value;
        public override bool Equals(object obj) => obj is DataRate other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => value.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(DataRate left, DataRate right) => left.Equals(right);
        public static bool operator !=(DataRate left, DataRate right) => !left.Equals(right);
    }
}
