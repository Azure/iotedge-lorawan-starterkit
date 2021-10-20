// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using LoRaWan;
    using Xunit;

    public class DevAddrTests
    {
        private readonly DevAddr subject = new(0xeb6f7bde);
        private readonly DevAddr other = new(0x12345678);

        [Fact]
        public void Size()
        {
            Assert.Equal(4, DevAddr.Size);
        }

        [Fact]
        public void NetworkId()
        {
            Assert.Equal(0x75, this.subject.NetworkId);
        }

        [Fact]
        public void NetworkAddress()
        {
            Assert.Equal(0x16f7bde, this.subject.NetworkAddress);
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
        public void Op_Equality_Returns_False_When_Values_Differ()
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
            Assert.Equal("EB6F7BDE", this.subject.ToString());
        }
    }
}
