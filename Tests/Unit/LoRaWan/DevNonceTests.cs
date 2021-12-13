// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Globalization;
    using LoRaWan;
    using Xunit;

    public class DevNonceTests
    {
        private readonly DevNonce subject = new(0x1234);
        private readonly DevNonce other = new(0x5678);

        [Fact]
        public void Size()
        {
            Assert.Equal(2, DevNonce.Size);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("4660", this.subject.ToString());
        }

        [Fact]
        public void CompareTo_Returns_Zero_For_Equal_Inputs()
        {
            Assert.Equal(0, this.subject.CompareTo(this.subject));
        }

        [Fact]
        public void CompareTo_Returns_Negative_Integer_When_Left_Is_Lesser()
        {
            Assert.Equal(-1, Math.Sign(this.subject.CompareTo(this.other)));
        }

        [Fact]
        public void CompareTo_Returns_Positive_Integer_When_Left_Is_Greater()
        {
            Assert.Equal(1, Math.Sign(this.other.CompareTo(this.subject)));
        }

        [Fact]
        public void Relational_Operators_Compare_Operands()
        {
            var lesser = this.subject;
            var greater = this.other;

            Assert.True(lesser < greater);
            Assert.False(greater < lesser);

            Assert.False(lesser > greater);
            Assert.True(greater > lesser);

#pragma warning disable CS1718 // Comparison made to same variable (unit tests for operator)
            Assert.True(lesser <= lesser);
            Assert.True(lesser <= greater);
            Assert.False(greater <= lesser);

            Assert.True(greater >= greater);
#pragma warning restore CS1718 // Comparison made to same variable
            Assert.True(greater >= lesser);
            Assert.False(lesser >= greater);
        }

        [Theory]
        [InlineData("{0}", "43794")]
        [InlineData("{0:G}", "43794")]
        [InlineData("{0:D}", "43794")]
        [InlineData("{0:g}", "43794")]
        [InlineData("{0:d}", "43794")]
        [InlineData("{0:N}", "12AB")]
        [InlineData("{0:n}", "12ab")]
        public void String_Interpolation_Success_Case(string format, string expected)
        {
            var subject = new DevNonce(0xAB12);
            var result = string.Format(CultureInfo.InvariantCulture, format, subject);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, "43794")]
        [InlineData("G", "43794")]
        [InlineData("D", "43794")]
        [InlineData("g", "43794")]
        [InlineData("d", "43794")]
        [InlineData("N", "12AB")]
        [InlineData("n", "12ab")]
        public void ToString_Returns_Correctly_Formatted_String(string format, string expectedRepresentation)
        {
            var subject = new DevNonce(0xAB12);
            Assert.Equal(expectedRepresentation, subject.ToString(format, null));
        }

        [Fact]
        public void ToString_Throws_On_Unsupported_Format()
        {
            _ = Assert.Throws<FormatException>(() => this.subject.ToString("foo", null));
        }

        [Fact]
        public void ToString_Parse_Preserves_Information()
        {
            // arrange
            var subject = new DevNonce(0xAB12);
            var hexString = "12AB";

            // act
            var obj = Parse(hexString);
            var stringResult = obj.ToString("N", null);
            var objResult = Parse(stringResult);

            // assert
            Assert.Equal(subject, objResult);
            Assert.Equal(hexString, stringResult);

            static DevNonce Parse(string input)
            {
                var buffer = new byte[DevNonce.Size];
                _ = Hexadecimal.TryParse(input, buffer);
                return DevNonce.Read(buffer);
            }
        }
    }
}
