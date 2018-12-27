
using System;
using LoRaTools.Utils;
using Xunit;

namespace LoRaWanTest
{
    public class ConversionHelperTest
    {
        [Theory]
        [InlineData(new byte[] { 1, 2, 3, 4}, "01020304")]
        [InlineData(new byte[] { 0xF1, 0xF2, 0xF3, 0xF4}, "F1F2F3F4")]
        public void Convert_Byte_Array_Should_Return_String(byte[] input, string expected)
        {
            var actual = ConversionHelper.ByteArrayToString(input);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] { 1, 2, 3, 4}, "01020304")]
        [InlineData(new byte[] { 0xF1, 0xF2, 0xF3, 0xF4}, "F1F2F3F4")]
        public void Convert_Byte_Memory_Should_Return_String(byte[] input, string expected)
        {
            var memory = new Memory<byte>(input);
            var actual = ConversionHelper.ByteArrayToString(memory);
            Assert.Equal(expected, actual);
        }
    }
}