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
    public readonly record struct DataRate
    {
        private readonly byte value;

        public DataRate(int value) =>
            this.value = value is var v and >= 0 and <= 15
                ? unchecked((byte)v)
                : throw new ArgumentOutOfRangeException(nameof(value), value, null);

        public int AsInt32 => this.value;

        public override string ToString() => this.value.ToString(CultureInfo.InvariantCulture);
    }
}
