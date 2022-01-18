// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using Common;
    using LoRaWan.NetworkServer;
    using Xunit;

    public class HexadecimalExtensionsTests
    {
        public static readonly TheoryData<string, byte[]> TestData =
            TheoryDataFactory.From(("01020304", new byte[] { 1, 2, 3, 4 }),
                                   ("F1F2F3F4", new byte[] { 0xF1, 0xF2, 0xF3, 0xF4 }),
                                   ("A1A2A3A4A5A6A7A8" +
                                    "B1B2B3B4B5B6B7B8" +
                                    "C1C2C3C4C5C6C7C8" +
                                    "D1D2D3D4D5D6D7D8" +
                                    "E1E2E3E4E5E6E7E8" +
                                    "F1F2F3F4F5F6F7F8",
                                    new byte[]
                                    {
                                        0xa1, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8,
                                        0xb1, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8,
                                        0xc1, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8,
                                        0xd1, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8,
                                        0xe1, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8,
                                        0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
                                    }));

        [Theory]
        [MemberData(nameof(TestData))]
        public void ToHex_With_Byte_Array_Returns_Hexadecimal_String(string expected, byte[] bytes)
        {
            var actual = bytes.ToHex();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void ToHex_With_Byte_Memory_Returns_Hexadecimal_String(string expected, byte[] bytes)
        {
            Memory<byte> memory = bytes;
            var actual = memory.ToHex();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void ToHex_With_Byte_ReadOnlyMemory_Returns_Hexadecimal_String(string expected, byte[] bytes)
        {
            ReadOnlyMemory<byte> memory = bytes;
            var actual = memory.ToHex();
            Assert.Equal(expected, actual);
        }
    }
}
