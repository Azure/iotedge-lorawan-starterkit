// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using LoRaWan;
    using Xunit;

    public class HertzTest
    {
        const ulong EuropeanFrequencyInHertz = 863_000_000;
        const ulong USFrequencyInHertz = 902_000_000;

        readonly Hertz MHz863 = new(EuropeanFrequencyInHertz);
        readonly Hertz MHz902 = new(USFrequencyInHertz);

        [Fact]
        public void HertzConversions_Behave_Properly()
        {
            Assert.Equal(863000, MHz863.Kilo);
            Assert.Equal(863, MHz863.Mega);
            Assert.Equal(0.863, MHz863.Giga);
        }

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = new Hertz(863_000_000);
            Assert.True(this.MHz863.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Value_Is_Different()
        {
            var other = this.MHz902;
            Assert.False(this.MHz863.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Different()
        {
            Assert.False(this.MHz863.Equals(new object()));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Null()
        {
            Assert.False(this.MHz863.Equals(null));
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Equal()
        {
            var other = new Hertz(EuropeanFrequencyInHertz);
            Assert.True(this.MHz863 == other);
        }

        [Fact]
        public void Op_Equality_Returns_False_When_Values_Differ()
        {
            Assert.False(this.MHz863 == this.MHz902);
        }

        [Fact]
        public void Op_Inequality_Returns_False_When_Values_Equal()
        {
            var other = new Hertz(EuropeanFrequencyInHertz);
            Assert.False(this.MHz863 != other);
        }

        [Fact]
        public void Op_Inequality_Returns_True_When_Values_Differ()
        {
            Assert.True(this.MHz863 != this.MHz902);
        }

        [Fact]
        public void ToString_Returns_Decimal_String()
        {
            Assert.Equal("863000000", this.MHz863.ToString());
        }
    }
}
