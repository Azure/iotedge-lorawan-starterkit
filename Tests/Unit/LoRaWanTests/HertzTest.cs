// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan;
    using Xunit;

    public class HertzTest
    {
        const ulong EuropeanFrequencyInHertz = 863_000_000;
        const ulong AmericanFrequencyInHertz = 902_000_000;

        readonly Hertz euFrequency = new(EuropeanFrequencyInHertz);
        readonly Hertz usFrequency = new(AmericanFrequencyInHertz);

        [Fact]
        public void HertzConversions_Behave_Properly()
        {
            Assert.Equal(863000, this.euFrequency.Kilo);
            Assert.Equal(863, this.euFrequency.Mega);
            Assert.Equal(0.863, this.euFrequency.Giga);
        }

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = new Hertz(863_000_000);
            Assert.True(this.euFrequency.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Value_Is_Different()
        {
            var other = this.usFrequency;
            Assert.False(this.euFrequency.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Different()
        {
            Assert.False(this.euFrequency.Equals(new object()));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Null()
        {
            Assert.False(this.euFrequency.Equals(null));
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Equal()
        {
            var other = new Hertz(EuropeanFrequencyInHertz);
            Assert.True(this.euFrequency == other);
        }

        [Fact]
        public void Op_Equality_Returns_False_When_Values_Differ()
        {
            Assert.False(this.euFrequency == this.usFrequency);
        }

        [Fact]
        public void Op_Inequality_Returns_False_When_Values_Equal()
        {
            var other = new Hertz(EuropeanFrequencyInHertz);
            Assert.False(this.euFrequency != other);
        }

        [Fact]
        public void Op_Inequality_Returns_True_When_Values_Differ()
        {
            Assert.True(this.euFrequency != this.usFrequency);
        }

        [Fact]
        public void ToString_Returns_Decimal_String()
        {
            Assert.Equal("863000000", this.euFrequency.ToString());
        }
    }
}
