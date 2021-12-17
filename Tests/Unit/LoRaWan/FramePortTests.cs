// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System.Linq;
    using LoRaWan.Tests.Common;
    using Xunit;
    using MoreEnumerable = MoreLinq.MoreEnumerable;

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

        public static readonly TheoryData<FramePort> AppFramePorts =
            TheoryDataFactory.From(MoreEnumerable.Generate(FramePort.AppMin, fport => fport + 1)
                                                 .TakeWhile(fport => fport <= FramePort.AppMax));

        [Theory]
        [InlineData(FramePorts.App1)]
        [InlineData(FramePorts.App10)]
        public void ApplicationLayerTestFPort_Should_Be_Flagged(FramePort fportValue)
        {
            Assert.True(fportValue.IsApplicationSpecific());
        }

        public static readonly TheoryData<FramePort> ReservedFramePorts =
            TheoryDataFactory.From(MoreEnumerable.Generate(FramePort.ReservedMin, fport => fport + 1)
                                                 .TakeWhile(fport => fport <= FramePort.ReservedMax));

        [Theory]
        [MemberData(nameof(ReservedFramePorts))]
        public void ReservedForFutureApplicationsTestFPort_Should_Be_Flagged(FramePort fportValue)
        {
            Assert.True(fportValue.IsReservedForFuture());
        }
    }
}
