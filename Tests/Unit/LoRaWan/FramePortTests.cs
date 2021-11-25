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
    }
}
