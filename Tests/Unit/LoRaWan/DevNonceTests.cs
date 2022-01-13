// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
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
    }
}
