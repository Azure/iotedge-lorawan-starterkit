// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using LoRaWan;
    using Xunit;

    public class DevNonceTests
    {
        readonly DevNonce subject = new(0x1234);
        readonly DevNonce other = new(0x5678);

        [Fact]
        public void Size()
        {
            Assert.Equal(2, DevNonce.Size);
        }

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = new DevNonce(subject.AsUInt16);
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
            var other = new DevNonce(subject.AsUInt16);
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
            var other = new DevNonce(subject.AsUInt16);
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
            Assert.Equal("4660", this.subject.ToString());
        }
    }
}
