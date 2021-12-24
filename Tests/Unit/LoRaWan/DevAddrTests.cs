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

        [Fact]
        public void NetworkIdMask_Is_7_MSB()
        {
            Assert.Equal(0xfe000000, DevAddr.NetworkIdMask);
        }

        [Fact]
        public void MaxNetworkId_Is_Largest_7_Bit_Integer()
        {
            Assert.Equal(0x7f, DevAddr.MaxNetworkId);
        }

        [Fact]
        public void NetworkAddressMask_Is_25_LSB()
        {
            Assert.Equal(0x01ff_ffffU, DevAddr.NetworkAddressMask);
        }

        [Fact]
        public void MaxNetworkAddress_Is_Largest_25_Bit_Integer()
        {
            Assert.Equal(0x1ff_ffff, DevAddr.MaxNetworkAddress);
        }

        [Fact]
        public void Init()
        {
            var networkId = 123;
            var networkAddress = 456;
            var result = new DevAddr(networkId, networkAddress);

            Assert.Equal(networkId, result.NetworkId);
            Assert.Equal(networkAddress, result.NetworkAddress);
        }

        [Theory]
        [InlineData(uint.MaxValue)]
        [InlineData(uint.MinValue)]
        [InlineData(0xeb6f7bde)]
        public void Constructor_NetworkId_NetworkAddress_Is_Equivalent_To_Value_Constructor(uint value)
        {
            // arrange
            var initial = new DevAddr(value);

            // act
            var result = new DevAddr(initial.NetworkId, initial.NetworkAddress);

            // assert
            Assert.Equal(initial, result);
        }

        [Theory]
        [InlineData("networkId", -1, 0)]
        [InlineData("networkId", 0x80, 0)]
        [InlineData("networkAddress", 0, -1)]
        [InlineData("networkAddress", 0, 0x0200_0000)]
        public void Init_Throws_When_Arg_Is_Invalid(string expectedParamName, int networkId, int networkAddress)
        {
            var ex = Assert.Throws<ArgumentException>(() => new DevAddr(networkId, networkAddress));
            Assert.Equal(expectedParamName, ex.ParamName);
        }

        [Fact]
        public void Size()
        {
            Assert.Equal(4, DevAddr.Size);
        }

        [Fact]
        public void NetworkId_Getter()
        {
            Assert.Equal(0x75, this.subject.NetworkId);
        }

        [Fact]
        public void NetworkId_Initter()
        {
            var newNetworkId = this.subject.NetworkId + 2;
            var result = this.subject with { NetworkId = newNetworkId };

            Assert.Equal(newNetworkId, result.NetworkId);
            Assert.Equal(this.subject.NetworkAddress, result.NetworkAddress);
        }

        [Fact]
        public void NetworkId_Initter_Throws_When_Input_Is_Invalid()
        {
            var ex = Assert.Throws<ArgumentException>(() => this.subject with { NetworkId = 0x80 });
            Assert.Equal("value", ex.ParamName);
        }

        [Fact]
        public void NetworkAddress_Getter()
        {
            Assert.Equal(0x16f7bde, this.subject.NetworkAddress);
        }

        [Fact]
        public void NetworkAddress_Initter()
        {
            var netNetworkAddress = this.subject.NetworkAddress + 2;
            var result = this.subject with { NetworkAddress = netNetworkAddress };

            Assert.Equal(netNetworkAddress, result.NetworkAddress);
            Assert.Equal(this.subject.NetworkId, result.NetworkId);
        }

        [Fact]
        public void NetworkAddress_Initter_Throws_When_Input_Is_Invalid()
        {
            var ex = Assert.Throws<ArgumentException>(() => this.subject with { NetworkAddress = 0x0200_00000 });
            Assert.Equal("value", ex.ParamName);
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

        public static TheoryData<string, uint> Parse_Data() =>
            TheoryDataFactory.From(("1234abcd", (uint)0x1234abcd),
                                   ("1234aBcd", (uint)0x1234abcd),
                                   ("1234ABCD", (uint)0x1234abcd));

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
