// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan.Tests.Common;
    using Xunit;

    public class FramePortTests
    {
        [Fact]
        public void UnderlyingType_Is_Byte()
        {
            Assert.Equal(typeof(byte), typeof(FramePort).GetEnumUnderlyingType());
        }

        [Fact]
        public void MacCommandFPort_Should_Be_Flagged()
        {
            Assert.Equal(0, (byte)FramePort.MacCommand);
        }

        [Fact]
        public void MacLayerTestFPort_Should_Be_Flagged()
        {
            Assert.Equal(224, (byte)FramePort.MacLayerTest);
        }

        [Theory]
        [InlineData(FramePorts.App1)]
        [InlineData(FramePorts.App10)]
        public void ApplicationLayerTestFPort_Should_Be_Flagged(FramePort fportValue)
        {
            Assert.True(fportValue.IsApplicationSpecific());
        }

        [Theory]
        [InlineData(FramePorts.ReservedMin)]
        [InlineData(FramePorts.ReservedMax)]
        public void ReservedForFutureApplicationsTestFPort_Should_Be_Flagged(FramePort fportValue)
        {
            Assert.True(fportValue.IsReservedForFuture());
        }
    }
}
