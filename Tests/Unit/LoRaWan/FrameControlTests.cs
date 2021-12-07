// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Linq;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;
    using MoreEnumerable = MoreLinq.MoreEnumerable;
    using static MoreLinq.Extensions.SubsetsExtension;
    using System.Collections.Immutable;

    public class FrameControlTests
    {
        private readonly FrameControl subject = new(FCtrlFlags.FPending, 5);

        [Fact]
        public void Size()
        {
            Assert.Equal(1, FrameControl.Size);
        }

        [Fact]
        public void Init_With_Byte()
        {
            var subjectCopy = new FrameControl((byte)this.subject);
            Assert.Equal(this.subject, subjectCopy);
        }

        // The following two types are purely for the purpose of making the in-line theory data for
        // the properties test more readable.

        private static class True
        {
            public const bool Adr = true;
            public const bool AdrAckRequested = true;
            public const bool Ack = true;
            public const bool DownlinkFramePending = true;
        }

        private static class False
        {
            public const bool Adr = false;
            public const bool AdrAckRequested = false;
            public const bool Ack = false;
            public const bool DownlinkFramePending = false;
        }

        public static readonly ImmutableArray<FCtrlFlags> FCtrlFlagsSubsets =
            Enum.GetValues<FCtrlFlags>()
                .Where(f => f != FCtrlFlags.None)
                .Subsets()
                .Select(fs => fs.Aggregate(FCtrlFlags.None, (fs, f) => fs | f))
                .ToImmutableArray();

        public static readonly TheoryData<FCtrlFlags, int, bool, bool, bool, bool> Property_Getter_Data =
            TheoryDataFactory.From(
                from flags in FCtrlFlagsSubsets
                from optionsLength in MoreEnumerable.Sequence(0, 15)
                select (flags, optionsLength, flags.HasFlag(FCtrlFlags.Adr),
                                              flags.HasFlag(FCtrlFlags.AdrAckReq),
                                              flags.HasFlag(FCtrlFlags.Ack),
                                              flags.HasFlag(FCtrlFlags.FPending)));

        [Theory]
        [MemberData(nameof(Property_Getter_Data))]
        public void Property_Getter(FCtrlFlags flags, int optionsLength,
                                    bool adr, bool adrAckRequested, bool ack, bool downlinkFramePending)
        {
            var subject = new FrameControl(flags, optionsLength);

            Assert.Equal(flags, subject.Flags);
            Assert.Equal(adr, subject.Adr);
            Assert.Equal(adrAckRequested, subject.AdrAckRequested);
            Assert.Equal(ack, subject.Ack);
            Assert.Equal(downlinkFramePending, subject.DownlinkFramePending);
            Assert.Equal(optionsLength, subject.OptionsLength);
        }

        public static readonly TheoryData<FCtrlFlags, FCtrlFlags, bool, bool, bool, bool> Flag_Property_Setter_Data =
            TheoryDataFactory.From(
                from init in FCtrlFlagsSubsets
                from expected in FCtrlFlagsSubsets
                select (expected, init, expected.HasFlag(FCtrlFlags.Adr),
                                        expected.HasFlag(FCtrlFlags.AdrAckReq),
                                        expected.HasFlag(FCtrlFlags.Ack),
                                        expected.HasFlag(FCtrlFlags.FPending)));

        [Theory]
        [MemberData(nameof(Flag_Property_Setter_Data))]
        public void Flag_Property_Setter(FCtrlFlags expectedFlags, FCtrlFlags initFlags, bool adr, bool adrAckRequested, bool ack, bool downlinkFramePending)
        {
            var subject = new FrameControl(initFlags, 15) with
            {
                Adr = adr,
                AdrAckRequested = adrAckRequested,
                Ack = ack,
                DownlinkFramePending = downlinkFramePending,
            };

            Assert.Equal(expectedFlags, subject.Flags);
            Assert.Equal(15, subject.OptionsLength);
        }

        public static readonly TheoryData<int> OneToFifteen = TheoryDataFactory.From(Enumerable.Range(1, 15));

        [Theory]
        [MemberData(nameof(OneToFifteen))]
        public void Init_Returns_Initialized_When_Options_Length_Is_Valid(int length)
        {
            var ex = Record.Exception(() => new FrameControl(FCtrlFlags.None, length));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(16)]
        public void Init_Throws_When_Options_Length_Is_Not_In_Range(int length)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new FrameControl(FCtrlFlags.None, length));
            Assert.Equal("optionsLength", ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(OneToFifteen))]
        public void Init_Throws_When_Flags_Is_Valid(int flags)
        {
            var ex = Assert.Throws<ArgumentException>(() => new FrameControl(checked((FCtrlFlags)flags), 0));
            Assert.Equal("flags", ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(OneToFifteen))]
        public void OptionsLength_Setter_Succeeds_When_Options_Length_Is_Valid(int length)
        {
            var ex = Record.Exception(() => this.subject with { OptionsLength = length });
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(16)]
        public void OptionsLength_Setter_Throws_When_Options_Length_Is_Not_In_Range(int length)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => this.subject with { OptionsLength = length });
            Assert.Equal("value", ex.ParamName);
        }

        public static readonly TheoryData<FCtrlFlags> FCtrlFlagsData = TheoryDataFactory.From(FCtrlFlagsSubsets);

        [Theory]
        [MemberData(nameof(FCtrlFlagsData))]
        public void Flags_Setter(FCtrlFlags flags)
        {
            var result = this.subject with { Flags = flags };

            Assert.Equal(flags, result.Flags);
            Assert.Equal(this.subject.OptionsLength, result.OptionsLength);
        }

        [Theory]
        [MemberData(nameof(FCtrlFlagsData))]
        public void Implicit_Conversion_To_FCtrlFlags(FCtrlFlags flags)
        {
            var subject = this.subject with { Flags = flags };
            FCtrlFlags result = subject;

            Assert.Equal(flags, result);
        }

        [Theory]
        [MemberData(nameof(OneToFifteen))]
        public void Flags_Setter_Throws_When_Value_Is_Invalid(int flags)
        {
            var ex = Assert.Throws<ArgumentException>(() => this.subject with { Flags = checked((FCtrlFlags)flags) });
            Assert.Equal("value", ex.ParamName);
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

        [Fact]
        public void Conversion_To_Byte_Returns_Byte_Encoding_Per_Spec()
        {
            Assert.Equal(21, (byte)this.subject);
        }
    }
}
