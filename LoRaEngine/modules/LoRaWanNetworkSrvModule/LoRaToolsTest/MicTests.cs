// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using LoRaWan;
    using Xunit;

    public class MicTests
    {
        readonly Mic subject = new(0x12345678);
        readonly Mic other = new(0x87654321);

        [Fact]
        public void Size()
        {
            Assert.Equal(4, Mic.Size);
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
            Assert.Equal("12345678", this.subject.ToString());
        }

        [Fact]
        public void Compute()
        {
            var joinEui = JoinEui.Parse("00-05-10-00-00-00-00-04");
            var devEui = DevEui.Parse("00-05-10-00-00-00-00-04");
            var devNonce = DevNonce.Read(new byte[] { 0xab, 0xcd });
            var appKey = AppKey.Parse("00000000000000000005100000000004");
            var mhdr = new MacHeader(0);
            var mic = Mic.ComputeForJoinRequest(appKey, mhdr, joinEui, devEui, devNonce);
            Assert.Equal(new Mic(0xb6dee36c), mic);
        }
    }
}
