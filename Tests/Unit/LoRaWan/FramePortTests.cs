// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System.Linq;
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
        public void MacCommand_Is_Zero()
        {
            Assert.Equal(0, (byte)FramePort.MacCommand);
        }

        [Fact]
        public void MacLayerTest_Is_224()
        {
            Assert.Equal(224, (byte)FramePort.MacLayerTest);
        }

        public static readonly TheoryData<bool, FramePort> AppFramePorts =
            TheoryDataFactory.From(from n in Enumerable.Range(0, 256)
                                   select (n is >= 1 and <= 223, checked((FramePort)n)));

        [Theory]
        [MemberData(nameof(AppFramePorts))]
        public void IsAppSpecific(bool expected, FramePort fport)
        {
            Assert.Equal(expected, fport.IsAppSpecific());
        }

        public static readonly TheoryData<bool, FramePort> ReservedFramePorts =
            TheoryDataFactory.From(from n in Enumerable.Range(0, 256)
                                   select (n is >= 225 and <= 255, checked((FramePort)n)));

        [Theory]
        [MemberData(nameof(ReservedFramePorts))]
        public void IsReserved(bool expected, FramePort fport)
        {
            Assert.Equal(expected, fport.IsReserved());
        }
    }
}
