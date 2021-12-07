// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Linq;
    using LoRaWan;
    using Xunit;

    public class FrameControlTests
    {
        private readonly FrameControl subject = new(FCtrlFlags.FPending, 5);

        [Fact]
        public void Size()
        {
            Assert.Equal(1, FrameControl.Size);
        }

        [Fact]
        public void Init_Returns_Initialized_When_Options_Length_Is_Valid()
        {
            foreach (var optionsLength in Enumerable.Range(0, 15))
            {
                var ex = Record.Exception(() => new FrameControl(FCtrlFlags.None, optionsLength));
                Assert.Null(ex);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(16)]
        public void Init_Throws_When_Options_Length_Is_Not_In_Range(int length)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new FrameControl(FCtrlFlags.None, length));
            Assert.Equal("optionsLength", ex.ParamName);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("15", this.subject.ToString());
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[1];
            var remainingBytes = this.subject.Write(bytes);
            Assert.Equal(0, remainingBytes.Length);
            Assert.Equal(new byte[] { 21 }, bytes);
        }
    }
}
