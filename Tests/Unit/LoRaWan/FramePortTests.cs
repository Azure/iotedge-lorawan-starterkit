// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using Xunit;

    public class FramePortTests
    {
        [Fact]
        public void Size()
        {
            Assert.Equal(1, FramePort.Size);
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

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        public void ApplicationLayerTestFPort_Should_Be_Flagged(byte fportValue)
        {
            Assert.True(new FramePort(fportValue).IsApplicationSpecificFPort);
        }

        [Theory]
        [InlineData(225)]
        [InlineData(255)]
        public void ReservedForFutureApplicationsTestFPort_Should_Be_Flagged(byte fportValue)
        {
            Assert.True(new FramePort(fportValue).IsReservedForFutureAplicationsFPort);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(224)]
        public void FPort_Can_Be_Cast_To_Byte(byte fportValue)
        {
            Assert.Equal(fportValue, (byte)new FramePort(fportValue));
        }
    }
}
