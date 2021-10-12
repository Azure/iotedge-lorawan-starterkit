// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using LoRaWan;
    using Xunit;

    public class AppKeyTests
    {
        readonly AppKey subject = AppKey.Parse("0123456789abcdeffedcba9876543210");
        readonly AppKey other = AppKey.Parse("fedcba98765432100123456789abcdef");

        [Fact]
        public void Size()
        {
            Assert.Equal(16, AppKey.Size);
        }

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = this.subject; // assignment = value copy semantics
            Assert.True(this.subject.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Value_Is_Different()
        {
            var other = this.other;
            Assert.False(this.subject.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Different()
        {
            Assert.False(this.subject.Equals(new object()));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Null()
        {
            Assert.False(this.subject.Equals(null));
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Equal()
        {
            var other = this.subject; // assignment = value copy semantics
            Assert.True(this.subject == other);
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Differ()
        {
            Assert.False(this.subject == this.other);
        }

        [Fact]
        public void Op_Inequality_Returns_False_When_Values_Equal()
        {
            var other = this.subject; // assignment = value copy semantics
            Assert.False(this.subject != other);
        }

        [Fact]
        public void Op_Inequality_Returns_True_When_Values_Differ()
        {
            Assert.True(this.subject != this.other);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("0123456789ABCDEFFEDCBA9876543210", this.subject.ToString());
        }

        [Fact]
        public void Parse_Returns_Parsed_Value_When_Input_Is_Valid()
        {
            var result = AppKey.Parse(this.subject.ToString());
            Assert.Equal(subject, result);
        }

        [Fact]
        public void Parse_Input_Is_Case_Insensitive()
        {
            var result = AppKey.Parse(this.subject.ToString().ToLowerInvariant());
            Assert.Equal(subject, result);
        }

        [Fact]
        public void Parse_Throws_When_Input_Is_Invalid()
        {
            _ = Assert.Throws<FormatException>(() => AppKey.Parse("foobar"));
        }

        [Fact]
        public void TryParse_Returns_True_And_Outputs_Parsed_Value_When_Input_Is_Valid()
        {
            var succeeded = AppKey.TryParse(this.subject.ToString(), out var result);
            Assert.True(succeeded);
            Assert.Equal(subject, result);
        }

        [Fact]
        public void TryParse_Input_Is_Case_Insensitive()
        {
            var succeeded = AppKey.TryParse(this.subject.ToString().ToLowerInvariant(), out var result);
            Assert.True(succeeded);
            Assert.Equal(subject, result);
        }

        [Fact]
        public void TryParse_Returns_False_When_Input_Is_Invalid()
        {
            var succeeded = AppKey.TryParse("foobar", out var result);
            Assert.False(succeeded);
            Assert.True(result == default);
        }
    }
}
