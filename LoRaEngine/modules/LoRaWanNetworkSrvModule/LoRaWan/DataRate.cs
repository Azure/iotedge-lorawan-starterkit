// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using static Bandwidth;
    using static SpreadingFactor;

    public enum ModulationKind { LoRa, Fsk }

    public abstract record DataRate
    {
        protected DataRate(ModulationKind modulationKind, object xpkDatrValue)
        {
            if (xpkDatrValue is null) throw new ArgumentNullException(nameof(xpkDatrValue));

            ModulationKind = modulationKind;
            XpkDatr = xpkDatrValue.ToString()!;
        }

        public ModulationKind ModulationKind { get; }

        /// <summary>
        /// The value for the <c>datr</c> member of <c>rxpk</c> or <c>txpk</c> JSON object from the
        /// communication protocol.
        /// </summary>
        /// <remarks>
        /// Technically, <c>datr</c> is either a string in case of a LoRa data rate or a number in
        /// case of an FSK data rate (see <a href="https://github.com/Lora-net/packet_forwarder/blob/c05eb0e16aef838b9404cb1acfd10112de43fdfd/PROTOCOL.TXT#L141-L142">
        /// the relevant section in the protocol documentation</a>), but this property is always a
        /// string because that is how <c>Rxpk.Datr</c> and <c>Txpk.Datr</c> are defined. This
        /// property should not be used except for backwards compatibility with code still relying
        /// on <c>Rxpk</c> and <c>Txpk</c>.
        /// </remarks>
        public string XpkDatr { get; }
    }

    public sealed record FskDataRate : DataRate
    {
        public static readonly FskDataRate Fsk50000 = new(50_000);

        private FskDataRate(int kbps) : base(ModulationKind.Fsk, kbps) =>
            BitRateInKiloBitsPerSecond = kbps;

        public int BitRateInKiloBitsPerSecond { get; }
    }

    public sealed record LoRaDataRate : DataRate
    {
        public static readonly LoRaDataRate SF7BW125 = new(SF7, BW125);
        public static readonly LoRaDataRate SF7BW250 = new(SF7, BW250);
        public static readonly LoRaDataRate SF7BW500 = new(SF7, BW500);

        public static readonly LoRaDataRate SF8BW125 = new(SF8, BW125);
        public static readonly LoRaDataRate SF8BW250 = new(SF8, BW250);
        public static readonly LoRaDataRate SF8BW500 = new(SF8, BW500);

        public static readonly LoRaDataRate SF9BW125 = new(SF9, BW125);
        public static readonly LoRaDataRate SF9BW250 = new(SF9, BW250);
        public static readonly LoRaDataRate SF9BW500 = new(SF9, BW500);

        public static readonly LoRaDataRate SF10BW125 = new(SF10, BW125);
        public static readonly LoRaDataRate SF10BW250 = new(SF10, BW250);
        public static readonly LoRaDataRate SF10BW500 = new(SF10, BW500);

        public static readonly LoRaDataRate SF11BW125 = new(SF11, BW125);
        public static readonly LoRaDataRate SF11BW250 = new(SF11, BW250);
        public static readonly LoRaDataRate SF11BW500 = new(SF11, BW500);

        public static readonly LoRaDataRate SF12BW125 = new(SF12, BW125);
        public static readonly LoRaDataRate SF12BW250 = new(SF12, BW250);
        public static readonly LoRaDataRate SF12BW500 = new(SF12, BW500);

        public static LoRaDataRate From(SpreadingFactor sf, Bandwidth bw) => (sf, bw) switch
        {
#pragma warning disable format
            (SF7 , BW125) => SF7BW125,
            (SF7 , BW250) => SF7BW250,
            (SF7 , BW500) => SF7BW500,
            (SF8 , BW125) => SF8BW125,
            (SF8 , BW250) => SF8BW250,
            (SF8 , BW500) => SF8BW500,
            (SF9 , BW125) => SF9BW125,
            (SF9 , BW250) => SF9BW250,
            (SF9 , BW500) => SF9BW500,
            (SF10, BW125) => SF10BW125,
            (SF10, BW250) => SF10BW250,
            (SF10, BW500) => SF10BW500,
            (SF11, BW125) => SF11BW125,
            (SF11, BW250) => SF11BW250,
            (SF11, BW500) => SF11BW500,
            (SF12, BW125) => SF12BW125,
            (SF12, BW250) => SF12BW250,
            (SF12, BW500) => SF12BW500,
            _ => throw new ArgumentException("Invalid spreading factor and/or bandwidth argument.")
#pragma warning restore format
        };

        private LoRaDataRate(SpreadingFactor sf, Bandwidth bw) :
            base(ModulationKind.LoRa, $"{sf}{bw}")
        {
            SpreadingFactor = (int)sf is >= 7 and <= 12 ? sf : throw new ArgumentOutOfRangeException(nameof(sf), sf, null);
            Bandwidth = (int)bw is 125 or 250 or 500 ? bw : throw new ArgumentOutOfRangeException(nameof(bw), bw, null);
        }

        public SpreadingFactor SpreadingFactor { get; }
        public Bandwidth Bandwidth { get; }

        public static LoRaDataRate Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, [NotNullWhen(true)] out LoRaDataRate? result)
        {
            result = null;
            if (input.Length is not 8 and not 9)
                return false;
            if (input[0] is not 'S' && input[1] is not 'F')
                return false;
            (var sf, var i) = (input[2], input[3]) switch
            {
                ('7', _) => (SF7, 3),
                ('8', _) => (SF8, 3),
                ('9', _) => (SF9, 3),
                ('1', '0') => (SF10, 4),
                ('1', '1') => (SF11, 4),
                ('1', '2') => (SF12, 4),
                _ => (default, 0),
            };
            if (i is 0)
                return false;
            input = input[i..];
            if (input.Length is not 5 && input[0] is not 'B' && input[1] is not 'W')
                return false;
            var bw = (input[2], input[3], input[4]) switch
            {
                ('1', '2', '5') => BW125,
                ('2', '5', '0') => BW250,
                ('5', '0', '0') => BW500,
                _ => (Bandwidth)0
            };
            result = (int)bw > 0 ? new LoRaDataRate(sf, bw) : null;
            return result is not null;
        }

        public override string ToString() => XpkDatr;
    }
}
