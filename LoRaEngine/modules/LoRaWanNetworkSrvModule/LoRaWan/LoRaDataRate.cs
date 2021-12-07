// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan
{
    using System;

    public readonly record struct LoRaDataRate
    {
        public LoRaDataRate(SpreadingFactor sf, Bandwidth bw)
        {
            SpreadingFactor = (int)sf is >= 7 and <= 12 ? sf : throw new ArgumentOutOfRangeException(nameof(sf), sf, null);
            Bandwidth = (int)bw is 125 or 250 or 500 ? bw : throw new ArgumentOutOfRangeException(nameof(bw), bw, null);
        }

        public SpreadingFactor SpreadingFactor { get; }
        public Bandwidth Bandwidth { get; }
        public bool IsUndefined => SpreadingFactor is SpreadingFactor.Undefined;

        public static LoRaDataRate Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out LoRaDataRate result)
        {
            result = default;
            if (input.Length is not 8 and not 9)
                return false;
            if (input[0] is not 'S' && input[1] is not 'F')
                return false;
            (var sf, var i) = (input[2], input[3]) switch
            {
                ('7', _) => (SpreadingFactor.SF7, 3),
                ('8', _) => (SpreadingFactor.SF8, 3),
                ('9', _) => (SpreadingFactor.SF9, 3),
                ('1', '0') => (SpreadingFactor.SF10, 4),
                ('1', '1') => (SpreadingFactor.SF11, 4),
                ('1', '2') => (SpreadingFactor.SF12, 4),
                _ => (SpreadingFactor.Undefined, 0),
            };
            if (sf is SpreadingFactor.Undefined)
                return false;
            input = input[i..];
            if (input.Length is not 5 && input[0] is not 'B' && input[1] is not 'W')
                return false;
            var bw = (input[2], input[3], input[4]) switch
            {
                ('1', '2', '5') => Bandwidth.BW125,
                ('2', '5', '0') => Bandwidth.BW250,
                ('5', '0', '0') => Bandwidth.BW500,
                _ => Bandwidth.Undefined
            };
            if (bw is Bandwidth.Undefined)
                return false;
            result = new LoRaDataRate(sf, bw);
            return true;
        }

        public override string ToString() => !IsUndefined ? $"{SpreadingFactor}{Bandwidth}" : string.Empty;
    }
}
