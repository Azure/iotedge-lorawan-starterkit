// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using Xunit;

    public class FramePortTests
    {
        private readonly FramePort subject = new(0);
        private readonly FramePort other = new(15);

        [Fact]
        public void Size()
        {
            Assert.Equal(1, FramePort.Size);
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
        public void MacCommandFPort_Should_Be_Flagged()
        {
            Assert.True(new FramePort(0).IsMacCommandFPort);
        }

        [Fact]
        public void MacLayerTestFPort_Should_Be_Flagged()
        {
            Assert.True(new FramePort(224).IsMacLayerTestFPort);
        }
    }
}
