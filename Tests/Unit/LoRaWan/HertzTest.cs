// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan;
    using Xunit;

    public class HertzTest
    {
        private readonly Hertz subject = new(863_000_000);

        [Fact]
        public void HertzConversions_Behave_Properly()
        {
            Assert.Equal(863000, this.subject.InKilo);
            Assert.Equal(863, this.subject.InMega);
            Assert.Equal(0.863, this.subject.InGiga);
        }

        [Fact]
        public void FromMega_Returns_Expected_Hertz()
        {
            var hz = Hertz.Mega(863.5);
            Assert.Equal(863500, hz.InKilo);
            Assert.Equal(863.5, hz.InMega);
            Assert.Equal(0.8635, hz.InGiga);
        }

        [Fact]
        public void ToString_Returns_Decimal_String()
        {
            Assert.Equal("863000000", this.subject.ToString());
        }
    }
}
