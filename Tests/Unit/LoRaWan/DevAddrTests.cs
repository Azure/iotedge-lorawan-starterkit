// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class DevAddrTests
    {
        private readonly DevAddr subject = new(0xeb6f7bde);

        [Theory]
        [InlineData(uint.MaxValue)]
        [InlineData(uint.MinValue)]
        [InlineData(0xeb6f7bde)]
        public void Init(uint value)
        {
            // arrange
            var maxValue = new DevAddr(value);

            // act
            var result = new DevAddr(maxValue.NetworkId, maxValue.NetworkAddress);

            // assert
            Assert.Equal(maxValue, result);
        }

        [Fact]
        public void Init_Validates_Limits()
        {
            var maxValue = new DevAddr(uint.MaxValue);

            Assert.Throws<ArgumentException>(() => new DevAddr(maxValue.NetworkId + 1, maxValue.NetworkAddress));
            Assert.Throws<ArgumentException>(() => new DevAddr(maxValue.NetworkId, maxValue.NetworkAddress + 1));
        }

        [Fact]
        public void Size()
        {
            Assert.Equal(4, DevAddr.Size);
        }

        [Fact]
        public void NetworkId()
        {
            Assert.Equal(0x75, this.subject.NetworkId);
        }

        [Fact]
        public void NetworkAddress()
        {
            Assert.Equal(0x16f7bde, this.subject.NetworkAddress);
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[4];
            var remainingBytes = this.subject.Write(bytes);
            Assert.Equal(0, remainingBytes.Length);
            Assert.Equal(new byte[] { 222, 123, 111, 235 }, bytes);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("EB6F7BDE", this.subject.ToString());
        }

        public static TheoryData<string, uint> Parse_Data() => TheoryDataFactory.From(new[]
        {
            ("1234abcd", (uint)0x1234abcd),
            ("1234aBcd", (uint)0x1234abcd),
            ("1234ABCD", (uint)0x1234abcd)
        });

        [Theory]
        [MemberData(nameof(Parse_Data))]
        public void Parse_Success(string input, uint expected)
        {
            var result = DevAddr.Parse(input);
            Assert.Equal(new DevAddr(expected), result);
        }

        [Theory]
        [MemberData(nameof(Parse_Data))]
        public void TryParse_Success(string input, uint expected)
        {
            Assert.True(DevAddr.TryParse(input, out var result));
            Assert.Equal(new DevAddr(expected), result);
        }

        public static TheoryData<string> Parse_Invalid_Data() =>
            TheoryDataFactory.From(new[] { "1234abcde", "1", string.Empty });

        [Theory]
        [MemberData(nameof(Parse_Invalid_Data))]
        public void Parse_Error(string input)
        {
            Assert.Throws<FormatException>(() => DevAddr.Parse(input));
        }

        [Theory]
        [MemberData(nameof(Parse_Invalid_Data))]
        public void TryParse_Error(string input)
        {
            Assert.False(DevAddr.TryParse(input, out var result));
            Assert.Equal(default, result);
        }

        [Fact]
        public void Parse_ToString_Preserves_Information()
        {
            // arrange
            var expected = new DevAddr(1);

            // act
            var result = DevAddr.Parse(expected.ToString());

            // assert
            Assert.Equal(expected, result);
        }
    }
}
