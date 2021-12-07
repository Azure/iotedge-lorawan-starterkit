// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class LoRaDataRateTests
    {
        [Fact]
        public void IsUndefined_Returns_True_For_Default_Value()
        {
            LoRaDataRate subject = default;
            Assert.True(subject.IsUndefined);
        }

        [Fact]
        public void Init_Throws_When_Spreading_Factor_Is_Undefined()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LoRaDataRate(SpreadingFactor.Undefined, Bandwidth.BW125));
            Assert.Equal("sf", ex.ParamName);
        }

        [Fact]
        public void Init_Throws_When_Bandwidth_Is_Undefined()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LoRaDataRate(SpreadingFactor.SF7, Bandwidth.Undefined));
            Assert.Equal("bw", ex.ParamName);
        }

        private static IEnumerable<T> LoRaDataRates<T>(Func<SpreadingFactor, Bandwidth, T> selector) =>
            from sf in Enum.GetValues<SpreadingFactor>()
            where sf is not SpreadingFactor.Undefined
            from bw in Enum.GetValues<Bandwidth>()
            where bw is not Bandwidth.Undefined
            select selector(sf, bw);

        public static readonly TheoryData<SpreadingFactor, Bandwidth> InitData =
            TheoryDataFactory.From(LoRaDataRates(ValueTuple.Create));

        [Theory]
        [MemberData(nameof(InitData))]
        public void Init(SpreadingFactor sf, Bandwidth bw)
        {
            var subject = new LoRaDataRate(sf, bw);

            Assert.Equal(sf, subject.SpreadingFactor);
            Assert.Equal(bw, subject.Bandwidth);
        }

        public static readonly TheoryData<string, SpreadingFactor, Bandwidth> ParseFormatData =
            TheoryDataFactory.From(LoRaDataRates((sf, bw) => ($"{sf}{bw}", sf, bw)));

        [Theory]
        [MemberData(nameof(ParseFormatData))]
#pragma warning disable xUnit1024 // Test methods cannot have overloads
        public void ToString(string expected, SpreadingFactor sf, Bandwidth bw)
#pragma warning restore xUnit1024 // Test methods cannot have overloads
        {
            var subject = new LoRaDataRate(sf, bw);

            Assert.Equal(expected, subject.ToString());
        }

        [Theory]
        [MemberData(nameof(ParseFormatData))]
        public void Parse(string input, SpreadingFactor sf, Bandwidth bw)
        {
            var subject = LoRaDataRate.Parse(input);

            Assert.Equal(sf, subject.SpreadingFactor);
            Assert.Equal(bw, subject.Bandwidth);
        }

        [Theory]
        [MemberData(nameof(ParseFormatData))]
        public void TryParse(string input, SpreadingFactor sf, Bandwidth bw)
        {
            var succeeded = LoRaDataRate.TryParse(input, out var subject);

            Assert.True(succeeded);
            Assert.Equal(sf, subject.SpreadingFactor);
            Assert.Equal(bw, subject.Bandwidth);
        }
    }
}
