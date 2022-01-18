// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using Xunit;

    public class LittleEndianReaderTests
    {
        [Theory]
        [InlineData(0x030201, new byte[] { 1, 2, 3 })]
        [InlineData(0x030201, new byte[] { 1, 2, 3, 4 })]
        public void ReadUInt24_Reads_Three_Bytes_In_Little_Endian(uint expected, byte[] buffer)
        {
            var actual = LittleEndianReader.ReadUInt24(buffer);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 1 })]
        [InlineData(new byte[] { 1, 2 })]
        public void ReadUInt24_Throws_When_Buffer_Length_Is_Too_Small(byte[] buffer)
        {
            var ex = Assert.Throws<ArgumentException>(() => LittleEndianReader.ReadUInt24(buffer));
            Assert.Equal("buffer", ex.ParamName);
        }
    }
}
