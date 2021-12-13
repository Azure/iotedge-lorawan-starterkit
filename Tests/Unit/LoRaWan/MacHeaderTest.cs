// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Linq;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class MacHeaderTest
    {
        private readonly MacHeader confirmedDataDown = new(128);
        private readonly MacHeader unconfirmedDataUp = new(64);

        public static readonly TheoryData<MacMessageType, DataMessageVersion> InitTestData =
            TheoryDataFactory.From(
                from t in Enum.GetValues<MacMessageType>()
                from v in Enum.GetValues<DataMessageVersion>()
                select (t, v));

        [Theory]
        [MemberData(nameof(InitTestData))]
        public void Init(MacMessageType messageType, DataMessageVersion major)
        {
            var subject = new MacHeader(messageType, major);

            Assert.Equal(messageType, subject.MessageType);
            Assert.Equal(major, subject.Major);
        }

        [Theory]
        [InlineData("messageType", -1, 0)]
        [InlineData("messageType", 8, 0)]
        [InlineData("major", 0, -1)]
        [InlineData("major", 0, 4)]
        public void Init_Throws_When_Arg_Is_Invalid(string expectedParamName, int messageType, int major)
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                _ = new MacHeader((MacMessageType)messageType, (DataMessageVersion)major));

            Assert.Equal(expectedParamName, ex.ParamName);
        }

        public static readonly TheoryData<MacMessageType, byte> MessageTypeTestData =
            TheoryDataFactory.From(
                from n in Enumerable.Range(0, 256)
                select checked((byte)n) into b
                select ((MacMessageType)(b >> 5), b));

        [Theory]
        [MemberData(nameof(MessageTypeTestData))]
        public void MessageType(MacMessageType expectedMessageType, byte input)
        {
            var subject = new MacHeader(input);
            var result = subject.MessageType;

            Assert.Equal(expectedMessageType, result);
        }

        public static readonly TheoryData<int, byte> MajorTestData =
            TheoryDataFactory.From(
                from n in Enumerable.Range(0, 256)
                select checked((byte)n) into b
                select (b & 0b11, b));

        [Theory]
        [MemberData(nameof(MajorTestData))]
        public void Major(int expectedMajor, byte input)
        {
            var subject = new MacHeader(input);
            var result = (int)subject.Major;

            Assert.Equal(expectedMajor, result);
        }

        [Fact]
        public void ToString_Returns_Hex_String()
        {
            Assert.Equal("80", this.confirmedDataDown.ToString());
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[4];
            var remainingBytes = this.unconfirmedDataUp.Write(bytes);
            remainingBytes.Fill(0xff);
            Assert.Equal(3, remainingBytes.Length);
            Assert.Equal(new byte[] { 0x40, 0xff, 0xff, 0xff }, bytes);
        }
    }
}
