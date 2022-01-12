// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class NetIdTests
    {
        private readonly NetId subject = new(0x1a2b3c);

        [Fact]
        public void Size()
        {
            Assert.Equal(3, NetId.Size);
        }

        [Theory]
        [InlineData(0x1a2b3c, 0x3c)]
        [InlineData(0xffffff, 0x7f)]
        public void NetworkId_Returns_7_Lsb(int netId, int expectedNetworkId)
        {
            var subject = new NetId(netId);
            Assert.Equal(expectedNetworkId, subject.NetworkId);
        }

        [Fact]
        public void Constructor_Should_Check_Allowed_Limits()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new NetId(0x0100_0000));
            Assert.Equal("value", ex.ParamName);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("1A2B3C", this.subject.ToString());
        }

        public static TheoryData<string, int> Parse_Data() =>
            TheoryDataFactory.From(("34abcd", 0x34abcd),
                                   ("0034aBcd", 0x34abcd),
                                   ("0034ABCD", 0x34abcd));

        [Theory]
        [MemberData(nameof(Parse_Data))]
        public void Parse_Success(string input, int expected)
        {
            var result = NetId.Parse(input);
            Assert.Equal(new NetId(expected), result);
        }

        [Theory]
        [MemberData(nameof(Parse_Data))]
        public void TryParse_Success(string input, int expected)
        {
            Assert.True(NetId.TryParse(input, out var result));
            Assert.Equal(new NetId(expected), result);
        }

        public static TheoryData<string> Parse_Invalid_Data() =>
           TheoryDataFactory.From(new[] { "1234abcde", "1g", string.Empty });

        [Theory]
        [MemberData(nameof(Parse_Invalid_Data))]
        public void Parse_Error(string input)
        {
            Assert.Throws<FormatException>(() => NetId.Parse(input));
        }

        [Theory]
        [MemberData(nameof(Parse_Invalid_Data))]
        public void TryParse_Error(string input)
        {
            Assert.False(NetId.TryParse(input, out var result));
            Assert.Equal(default, result);
        }

        [Fact]
        public void TryParse_Does_Not_Throw_When_Argument_Out_Of_Range()
        {
            Assert.False(NetId.TryParse("FFFFFFFF", out var _));
        }

        [Fact]
        public void Parse_ToString_Preserves_Information()
        {
            var expected = new NetId(1);
            var result = NetId.Parse(expected.ToString());
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[3];
            var remainingBytes = this.subject.Write(bytes);
            Assert.Equal(0, remainingBytes.Length);
            Assert.Equal(new byte[] { 0x3c, 0x2b, 0x1a }, bytes);
        }

        [Fact]
        public void Read_Success()
        {
            var bytes = new byte[] { 0xaa, 0xab, 0xac };
            var result = NetId.Read(bytes);
            Assert.Equal(new NetId(0xacabaa), result);
        }

        [Fact]
        public void Write_Read_Preserves_Information()
        {
            var buffer = new byte[NetId.Size];
            _ = subject.Write(buffer);
            Assert.Equal(this.subject, NetId.Read(buffer));
        }
    }
}
