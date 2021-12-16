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
    }
}
