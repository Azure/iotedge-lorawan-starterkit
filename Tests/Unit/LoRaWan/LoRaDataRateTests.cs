// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class LoRaDataRateTests
    {
        private static IEnumerable<T> LoRaDataRates<T>(Func<SpreadingFactor, Bandwidth, T> selector) =>
            from sf in Enum.GetValues<SpreadingFactor>()
            from bw in Enum.GetValues<Bandwidth>()
            select selector(sf, bw);

        public static readonly TheoryData<SpreadingFactor, Bandwidth> InitData =
            TheoryDataFactory.From(LoRaDataRates(ValueTuple.Create));

        [Theory]
        [MemberData(nameof(InitData))]
        public void From(SpreadingFactor sf, Bandwidth bw)
        {
            var subject = LoRaDataRate.From(sf, bw);

            Assert.Equal(sf, subject.SpreadingFactor);
            Assert.Equal(bw, subject.Bandwidth);
            Assert.Equal(ModulationKind.LoRa, subject.ModulationKind);
        }

        public static readonly TheoryData<string, SpreadingFactor, Bandwidth> ParseFormatData =
            TheoryDataFactory.From(LoRaDataRates((sf, bw) => ($"{sf}{bw}", sf, bw)));

        [Theory]
        [MemberData(nameof(ParseFormatData))]
#pragma warning disable xUnit1024 // Test methods cannot have overloads
        public void ToString(string expected, SpreadingFactor sf, Bandwidth bw)
#pragma warning restore xUnit1024 // Test methods cannot have overloads
        {
            var subject = LoRaDataRate.From(sf, bw);

            Assert.Equal(expected, subject.ToString());
        }

        [Theory]
        [MemberData(nameof(ParseFormatData))]
        public void Parse(string input, SpreadingFactor sf, Bandwidth bw)
        {
            var subject = LoRaDataRate.Parse(input);

            Assert.Equal(sf, subject.SpreadingFactor);
            Assert.Equal(bw, subject.Bandwidth);
            Assert.Same(LoRaDataRate.From(sf, bw), subject);
        }

        [Theory]
        [MemberData(nameof(ParseFormatData))]
        public void TryParse(string input, SpreadingFactor sf, Bandwidth bw)
        {
            var succeeded = LoRaDataRate.TryParse(input, out var subject);

            Assert.True(succeeded);
            Assert.Equal(sf, subject.SpreadingFactor);
            Assert.Equal(bw, subject.Bandwidth);
            Assert.Same(LoRaDataRate.From(sf, bw), subject);
        }

        public static readonly TheoryData<string> InvalidParseData =
            TheoryDataFactory.From("",
                                   "SF6BW125",
                                   "SF7BW555",
                                   "SS7BB125");

        [Theory]
        [MemberData(nameof(InvalidParseData))]
        public void Parse_Failure(string input)
        {
            Assert.Throws<FormatException>(() => LoRaDataRate.Parse(input));
        }

        [Fact]
        public void Parse_Throws_When_Input_Is_Null()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => LoRaDataRate.Parse(null));

            Assert.Equal("input", ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(InvalidParseData))]
        public void TryParse_Failure(string input)
        {
            var succeeded = LoRaDataRate.TryParse(input, out _);

            Assert.False(succeeded);
        }

        [Fact]
        public void TryParse_Returns_False_When_Input_Is_Null()
        {
            var succeeded = LoRaDataRate.TryParse(null, out _);

            Assert.False(succeeded);
        }
    }
}
