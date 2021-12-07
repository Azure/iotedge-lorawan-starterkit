// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Linq;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static MoreLinq.Extensions.SubsetsExtension;
    using MoreEnumerable = MoreLinq.MoreEnumerable;

    public class FrameControlTests
    {
        [Fact]
        public void Size()
        {
            Assert.Equal(1, FrameControl.Size);
        }

        public static readonly TheoryData<byte, FCtrlFlags, int> Codec_Test_Data =
            TheoryDataFactory.From(
                from flags in
                    from fs in Enum.GetValues<FCtrlFlags>()
                                   .Where(f => f != FCtrlFlags.None)
                                   .Subsets()
                    select fs.Aggregate(FCtrlFlags.None, (fs, f) => fs | f)
                from optionsLength in MoreEnumerable.Sequence(0, 15)
                select (checked((byte)((byte)flags | optionsLength)), flags, optionsLength));

        [Theory]
        [MemberData(nameof(Codec_Test_Data))]
        public void Encode(byte exptected, FCtrlFlags flags, int optionsLength)
        {
            var result = FrameControl.Encode(flags, optionsLength);
            Assert.Equal(exptected, result);
        }

        [Theory]
        [MemberData(nameof(Codec_Test_Data))]
        public void Decode(byte input, FCtrlFlags expectedFlags, int expectedOptionsLength)
        {
            var (flags, optionsLength) = FrameControl.Decode(input);
            Assert.Equal(expectedFlags, flags);
            Assert.Equal(expectedOptionsLength, optionsLength);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(16)]
        public void Encode_Throws_When_Options_Length_Is_Not_In_Range(int length)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => FrameControl.Encode(FCtrlFlags.None, length));
            Assert.Equal("optionsLength", ex.ParamName);
        }

        public static readonly TheoryData<int> Encode_Throws_When_Flags_Is_Invalid_Data =
            TheoryDataFactory.From(MoreEnumerable.Sequence(1, 15));

        [Theory]
        [MemberData(nameof(Encode_Throws_When_Flags_Is_Invalid_Data))]
        public void Encode_Throws_When_Flags_Is_Invalid(byte flags)
        {
            var ex = Assert.Throws<ArgumentException>(() => FrameControl.Encode((FCtrlFlags)flags, 0));
            Assert.Equal("flags", ex.ParamName);
        }
    }
}
