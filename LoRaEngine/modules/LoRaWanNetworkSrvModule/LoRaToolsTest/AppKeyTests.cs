// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using LoRaWan;
    using Xunit;

    public class AppKeyTests
    {
        readonly AppKey subject = new(new UInt128(0x0123456789abcdef, 0xfedcba9876543210));
        readonly AppKey other = new(new UInt128(0xfedcba9876543210, 0x0123456789abcdef));

        [Fact]
        public void Size()
        {
            Assert.Equal(16, AppKey.Size);
        }

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = new AppKey(subject.AsUInt128);
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
            var other = new AppKey(subject.AsUInt128);
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
            var other = new AppKey(subject.AsUInt128);
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
    }
}
