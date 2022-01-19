// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Linq;
    using Xunit;

    public class AppNonceTests
    {
        private readonly AppNonce subject = new(0x123456);

        [Fact]
        public void Size_Is_3_Bytes()
        {
            Assert.Equal(3, AppNonce.Size);
        }

        [Fact]
        public void MaxValue_Is_Largest_24_Bit_Integer()
        {
            Assert.Equal(16777215, AppNonce.MaxValue);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(AppNonce.MaxValue + 1)]
        public void Init_Throws_For_Invalid_Value(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AppNonce(value));
        }

        [Fact]
        public void ToString_Returns_Decimal_String()
        {
            Assert.Equal("1193046", this.subject.ToString());
        }

        [Fact]
        public void Conversion_To_Int_Returns_Value()
        {
            var result = (int)this.subject;
            Assert.Equal(1193046, result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Read_Throws_When_Buffer_Is_Too_Small(int size)
        {
            var ex = Assert.Throws<ArgumentException>(() => AppNonce.Read(new byte[size]));
            Assert.Equal("Insufficient buffer length. (Parameter 'buffer')", ex.Message);
        }

        [Theory]
        [InlineData(0x123456, new byte[] { 0x56, 0x34, 0x12 })]
        [InlineData(0x123456, new byte[] { 0x56, 0x34, 0x12, 0xff })]
        public void Read_Decodes_In_Little_Endian(int expected, byte[] bytes)
        {
            var result = AppNonce.Read(bytes);
            Assert.Equal(new AppNonce(expected), result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Write_Throws_When_Buffer_Is_Too_Small(int size)
        {
            var ex = Assert.Throws<ArgumentException>(() => this.subject.Write(new byte[size]));
            Assert.Equal("Insufficient buffer length.", ex.Message);
        }

        [Theory]
        [InlineData(new byte[] { 0x56, 0x34, 0x12 }, 0x123456)]
        public void Write_Encodes_In_Little_Endian(byte[] expected, int input)
        {
            const byte fill = 0xff;
            var buffer = Enumerable.Repeat(fill, 10).ToArray();
            var nonce = new AppNonce(input);

            var result = nonce.Write(buffer);

            Assert.Equal(expected, buffer[..3]);
            Assert.Equal(Enumerable.Repeat(fill, 7).ToArray(), result.ToArray());
        }
    }
}
