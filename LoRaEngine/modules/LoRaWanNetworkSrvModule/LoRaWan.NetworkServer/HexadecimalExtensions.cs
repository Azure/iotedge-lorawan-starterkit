// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;

    internal static class HexadecimalExtensions
    {
        public static string ToHex(this byte[] bytes) => FormatHexadecimal(bytes);
        public static string ToHex(this Memory<byte> bytes) => FormatHexadecimal(bytes);
        public static string ToHex(this ReadOnlyMemory<byte> bytes) => FormatHexadecimal(bytes);

        private static string FormatHexadecimal(ReadOnlyMemory<byte> bytes)
        {
            var charLength = bytes.Length * 2;
            var chars = charLength <= 64 ? stackalloc char[charLength] : new char[charLength];
            Hexadecimal.Write(bytes.Span, chars);
            return new string(chars);
        }
    }
}
