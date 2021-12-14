// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class HexadecimalTests
    {
        [Theory]
        [InlineData(0x00, LetterCase.Upper, "00")]
        [InlineData(0x01, LetterCase.Upper, "01")]
        [InlineData(0x12, LetterCase.Upper, "12")]
        [InlineData(0x9a, LetterCase.Upper, "9A")]
        [InlineData(0xff, LetterCase.Upper, "FF")]
        [InlineData(0x00, LetterCase.Lower, "00")]
        [InlineData(0x01, LetterCase.Lower, "01")]
        [InlineData(0x12, LetterCase.Lower, "12")]
        [InlineData(0x9a, LetterCase.Lower, "9a")]
        [InlineData(0xff, LetterCase.Lower, "ff")]
        public void Write_Byte(byte input, LetterCase letterCase, string expected)
        {
            var chars = new char[2];
            Hexadecimal.Write(input, chars, letterCase);
            Assert.Equal(expected, new string(chars));
        }

        [Fact]
        public void Write_Byte_Throws_When_Buffer_Is_Too_Small()
        {
            var chars = new char[1];
            var ex = Assert.Throws<ArgumentException>(() => Hexadecimal.Write(0xff, chars));
            Assert.Equal("output", ex.ParamName);
        }

        [Theory]
        [InlineData(0x0000, LetterCase.Upper, "0000")]
        [InlineData(0x0001, LetterCase.Upper, "0001")]
        [InlineData(0x0012, LetterCase.Upper, "0012")]
        [InlineData(0x0123, LetterCase.Upper, "0123")]
        [InlineData(0x1234, LetterCase.Upper, "1234")]
        [InlineData(0x9abc, LetterCase.Upper, "9ABC")]
        [InlineData(0xffff, LetterCase.Upper, "FFFF")]
        [InlineData(0x0000, LetterCase.Lower, "0000")]
        [InlineData(0x0001, LetterCase.Lower, "0001")]
        [InlineData(0x0012, LetterCase.Lower, "0012")]
        [InlineData(0x0123, LetterCase.Lower, "0123")]
        [InlineData(0x1234, LetterCase.Lower, "1234")]
        [InlineData(0x9abc, LetterCase.Lower, "9abc")]
        [InlineData(0xffff, LetterCase.Lower, "ffff")]
        public void Write_UInt16(ushort input, LetterCase letterCase, string expected)
        {
            var chars = new char[4];
            Hexadecimal.Write(input, chars, letterCase);
            Assert.Equal(expected, new string(chars));
        }

        [Fact]
        public void Write_UInt16_Throws_When_Buffer_Is_Too_Small()
        {
            var chars = new char[1];
            var ex = Assert.Throws<ArgumentException>(() => Hexadecimal.Write(0xffff, chars));
            Assert.Equal("output", ex.ParamName);
        }

        [Theory]
        [InlineData(0x0000000000000000UL, LetterCase.Upper, "0000000000000000")]
        [InlineData(0x0123456789abcdefUL, LetterCase.Upper, "0123456789ABCDEF")]
        [InlineData(0x0000000000000000UL, LetterCase.Lower, "0000000000000000")]
        [InlineData(0x0123456789abcdefUL, LetterCase.Lower, "0123456789abcdef")]
        public void Write_UInt64(ulong input, LetterCase letterCase, string expected)
        {
            var chars = new char[16];
            Hexadecimal.Write(input, chars, letterCase);
            Assert.Equal(expected, new string(chars));
        }

        [Fact]
        public void Write_UInt64_Throws_When_Buffer_Is_Too_Small()
        {
            var chars = new char[1];
            var ex = Assert.Throws<ArgumentException>(() => Hexadecimal.Write(0UL, chars));
            Assert.Equal("output", ex.ParamName);
        }

        [Theory]
        [InlineData(new byte[0], LetterCase.Upper, null, "")]
        [InlineData(new byte[0], LetterCase.Upper, '-', "")]
        [InlineData(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef }, LetterCase.Lower, null, "0123456789abcdef")]
        [InlineData(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef }, LetterCase.Upper, null, "0123456789ABCDEF")]
        [InlineData(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef }, LetterCase.Lower, '-', "01-23-45-67-89-ab-cd-ef")]
        [InlineData(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef }, LetterCase.Upper, '-', "01-23-45-67-89-AB-CD-EF")]
        [InlineData(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef }, LetterCase.Upper, ':', "01:23:45:67:89:AB:CD:EF")]
        [InlineData(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef }, LetterCase.Upper, ',', "01,23,45,67,89,AB,CD,EF")]
        public void Write(byte[] input, LetterCase @case, char? separator, string expected)
        {
            var chars = new char[expected.Length];
            Hexadecimal.Write(input, chars, separator, @case);
            var actual = new string(chars);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1, 0, null)]
        [InlineData(1, 1, null)]
        [InlineData(2, 0, '-')]
        [InlineData(2, 1, '-')]
        [InlineData(2, 2, '-')]
        [InlineData(2, 3, '-')]
        public void Write_Throws_When_Buffer_Is_Too_Small(int byteCount, int charCount, char? separator)
        {
            var chars = new char[charCount];
            var ex = Assert.Throws<ArgumentException>(() => Hexadecimal.Write(new byte[byteCount], chars, separator));
            Assert.Equal("output", ex.ParamName);
        }

        [Theory]
        [InlineData("", null, new byte[0])]
        [InlineData("", '-', new byte[0])]
        [InlineData("0123456789abcdef", null, new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        [InlineData("0123456789aBcDeF", null, new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        [InlineData("0123456789ABCDEF", null, new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        [InlineData("01-23-45-67-89-ab-cd-ef", '-', new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        [InlineData("01-23-45-67-89-ab-CD-ef", '-', new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        [InlineData("01-23-45-67-89-AB-CD-EF", '-', new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        [InlineData("01:23:45:67:89:AB:CD:EF", ':', new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        [InlineData("01,23,45,67,89,AB,CD,EF", ',', new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef })]
        public void TryParse_With_Valid_Input(string input, char? separator, byte[] expected)
        {
            var actual = new byte[expected.Length];
            var succeeded = Hexadecimal.TryParse(input, actual, separator);
            Assert.True(succeeded);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("01aB", null, 19114957)]
        [InlineData("01AB", null, 19114957)]
        [InlineData("01-ab", '-', 19114957)]
        [InlineData("01-aB", '-', 19114957)]
        [InlineData("01-AB", '-', 19114957)]
        [InlineData("01:AB", ':', 19114957)]
        [InlineData("01,AB", ',', 19114957)]
        public void TryParse_UInt16_With_Valid_Input(string input, char? separator, ushort expected)
        {
            var succeeded = Hexadecimal.TryParse(input, out ushort actual, separator);
            Assert.True(succeeded);
            Assert.Equal(expected, actual);
        }

        public static TheoryData<string, char?> InvalidInput() => TheoryDataFactory.From(new[]
        {
            ("1", (char?)null),
            ("l2", null),
            ("123", null),
            ("12345", null),
            ("12:34", '-'),
            ("12--34", '-'),
            ("123-456", '-'),
            ("1", '-'),
            ("123", '-'),
            ("12345", '-'),
            ("12-", '-'),
            ("-12", '-'),
            ("-12-", '-'),
            ("12-34-", '-'),
            ("12:E4:S6", ':')
        });

        [Theory]
        [MemberData(nameof(InvalidInput))]
        public void TryParse_UInt16_With_Invalid_Input(string input, char? separator)
        {
            Assert.False(Hexadecimal.TryParse(input, out ushort _, separator));
        }

        [Theory]
        [MemberData(nameof(InvalidInput))]
        public void TryParse_With_Invalid_Input(string input, char? separator)
        {
            var succeeded = Hexadecimal.TryParse(input, new byte[100], separator);
            Assert.False(succeeded);
        }

        [Fact]
        public void TryParse_Throws_When_Buffer_Is_Too_Small()
        {
            var buffer = new byte[1];
            var ex = Assert.Throws<ArgumentException>(() => Hexadecimal.TryParse("1234", buffer));
            Assert.Equal("output", ex.ParamName);
        }
    }
}
