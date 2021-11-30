// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using LoRaWan;
    using LoRaWan.Tests.Common;
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
        public void None_Is_Initialized_To_Defaults()
        {
            var subject = FrameControl.None;

            Assert.Equal(FCtrlFlags.None, subject.Flags);
            Assert.Equal(0, subject.OptionsLength);
            Assert.Equal(default, subject);
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

        private const FCtrlFlags AllFCtrlFlags = FCtrlFlags.Adr | FCtrlFlags.AdrAckReq | FCtrlFlags.Ack | FCtrlFlags.FPending;

        [Theory]
        [InlineData(FCtrlFlags.None     , 0, False.Adr, False.AdrAckRequested, False.Ack, False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.None     , 3, False.Adr, False.AdrAckRequested, False.Ack, False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.Adr      , 0, True.Adr , False.AdrAckRequested, False.Ack, False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.AdrAckReq, 0, False.Adr, True.AdrAckRequested , False.Ack, False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.Ack      , 0, False.Adr, False.AdrAckRequested, True.Ack , False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.FPending , 0, False.Adr, False.AdrAckRequested, False.Ack, True.DownlinkFramePending )]
        [InlineData(AllFCtrlFlags       , 0, True.Adr , True.AdrAckRequested , True.Ack , True.DownlinkFramePending )]
        [InlineData(AllFCtrlFlags       , 5, True.Adr , True.AdrAckRequested , True.Ack , True.DownlinkFramePending )]
        public void Properties(FCtrlFlags flags, int optionsLength,
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

        public static readonly TheoryData<int> OneToFifteen = new()
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
        };

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

        [Theory]
        [MemberData(nameof(TheoryDataFactory.GetEnumMembers), typeof(FCtrlFlags), MemberType = typeof(TheoryDataFactory))]
        public void Flags_Setter(FCtrlFlags flags)
        {
            var result = this.subject with { Flags = flags };

            Assert.Equal(flags, result.Flags);
            Assert.Equal(this.subject.OptionsLength, result.OptionsLength);
        }

        [Theory]
        [MemberData(nameof(TheoryDataFactory.GetEnumMembers), typeof(FCtrlFlags), MemberType = typeof(TheoryDataFactory))]
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

        [Theory]
        [InlineData(FCtrlFlags.None, FCtrlFlags.Adr      , True.Adr , False.AdrAckRequested, False.Ack, False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.None, FCtrlFlags.AdrAckReq, False.Adr, True.AdrAckRequested , False.Ack, False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.None, FCtrlFlags.Ack      , False.Adr, False.AdrAckRequested, True.Ack , False.DownlinkFramePending)]
        [InlineData(FCtrlFlags.None, FCtrlFlags.FPending , False.Adr, False.AdrAckRequested, False.Ack, True.DownlinkFramePending )]
        public void Flag_Property_Setter(FCtrlFlags initFlags, FCtrlFlags expectedFlags, bool adr, bool adrAckRequested, bool ack, bool downlinkFramePending)
        {
            var subject = new FrameControl(initFlags, 5) with
            {
                Adr = adr,
                AdrAckRequested = adrAckRequested,
                Ack = ack,
                DownlinkFramePending = downlinkFramePending,
            };

            Assert.Equal(adr, subject.Adr);
            Assert.Equal(adrAckRequested, subject.AdrAckRequested);
            Assert.Equal(ack, subject.Ack);
            Assert.Equal(downlinkFramePending, subject.DownlinkFramePending);
            Assert.Equal(expectedFlags, subject.Flags);
            Assert.Equal(5, subject.OptionsLength);
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
