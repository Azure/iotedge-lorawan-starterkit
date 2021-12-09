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

        [Fact]
        public void Conversion_To_UInt64_Returns_Initial_Value()
        {
            Assert.Equal(863_000_000UL, (ulong)this.subject);
        }

        [Theory]
        [InlineData(-1, 123, 456)]
        [InlineData( 1, 456, 123)]
        [InlineData( 0, 123, 123)]
        public void CompareTo(int expected, ulong a, ulong b)
        {
            var subject1 = new Hertz(a);
            var subject2 = new Hertz(b);

            Assert.Equal(expected, subject1.CompareTo(subject2));
        }

        [Theory]
        [InlineData(true , 123, 456)]
        [InlineData(false, 456, 123)]
        [InlineData(false, 123, 123)]
        public void LessThan(bool expected, ulong a, ulong b)
        {
            var subject1 = new Hertz(a);
            var subject2 = new Hertz(b);

            Assert.Equal(expected, subject1 < subject2);
        }

        [Theory]
        [InlineData(true , 123, 456)]
        [InlineData(false, 456, 123)]
        [InlineData(true , 123, 123)]
        public void LessThanOrEqual(bool expected, ulong a, ulong b)
        {
            var subject1 = new Hertz(a);
            var subject2 = new Hertz(b);

            Assert.Equal(expected, subject1 <= subject2);
        }

        [Theory]
        [InlineData(false, 123, 456)]
        [InlineData(true , 456, 123)]
        [InlineData(false, 123, 123)]
        public void GreaterThan(bool expected, ulong a, ulong b)
        {
            var subject1 = new Hertz(a);
            var subject2 = new Hertz(b);

            Assert.Equal(expected, subject1 > subject2);
        }

        [Theory]
        [InlineData(false, 123, 456)]
        [InlineData(true , 456, 123)]
        [InlineData(true , 123, 123)]
        public void GreaterThanOrEqual(bool expected, ulong a, ulong b)
        {
            var subject1 = new Hertz(a);
            var subject2 = new Hertz(b);

            Assert.Equal(expected, subject1 >= subject2);
        }
    }
}
