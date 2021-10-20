// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Linq;
    using LoRaWan;
    using Xunit;

    public class FrameControlTests
    {
        private readonly FrameControl subject = new(FCtrlFlags.FPending, 5);
        private readonly FrameControl other = new(FCtrlFlags.Ack, 0);

        [Fact]
        public void Size()
        {
            Assert.Equal(1, FrameControl.Size);
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
        [InlineData(FCtrlFlags.None         , 0, False.Adr, False.AdrAckRequested, False.Ack, False.DownlinkFramePending, 0)]
        [InlineData(FCtrlFlags.None         , 3, False.Adr, False.AdrAckRequested, False.Ack, False.DownlinkFramePending, 3)]
        [InlineData(FCtrlFlags.Adr          , 0, True.Adr , False.AdrAckRequested, False.Ack, False.DownlinkFramePending, 0)]
        [InlineData(FCtrlFlags.AdrAckReq    , 0, False.Adr, True.AdrAckRequested , False.Ack, False.DownlinkFramePending, 0)]
        [InlineData(FCtrlFlags.Ack          , 0, False.Adr, False.AdrAckRequested, True.Ack , False.DownlinkFramePending, 0)]
        [InlineData(FCtrlFlags.FPending     , 0, False.Adr, False.AdrAckRequested, False.Ack, True.DownlinkFramePending , 0)]
        [InlineData(AllFCtrlFlags           , 0, True.Adr , True.AdrAckRequested , True.Ack , True.DownlinkFramePending , 0)]
        [InlineData(AllFCtrlFlags           , 5, True.Adr , True.AdrAckRequested , True.Ack , True.DownlinkFramePending , 5)]
        public void Properties(FCtrlFlags flags, int initOptionsLength,
                               bool adr, bool adrAckRequested, bool ack, bool downlinkFramePending, int optionsLength)
        {
            var subject = new FrameControl(flags, initOptionsLength);
            Assert.Equal(adr, subject.Adr);
            Assert.Equal(adrAckRequested, subject.AdrAckRequested);
            Assert.Equal(ack, subject.Ack);
            Assert.Equal(downlinkFramePending, subject.DownlinkFramePending);
            Assert.Equal(optionsLength, subject.OptionsLength);
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
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("15", this.subject.ToString());
        }
    }
}
