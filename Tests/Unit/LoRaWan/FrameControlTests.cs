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
