// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    static class Hexadecimal
    {
        const string Digits = "0123456789ABCDEF";

        public static void Write(ulong value, Span<char> buffer)
        {
            var ci = buffer.Length;
            for (var i = 0; i < 16; i++)
            {
                buffer[--ci] = Digits[(int)(value & 0x0000000f)];
                value >>= 4;
            }
        }

        public static bool TryParse(ReadOnlySpan<char> chars, out ulong value, char? separator = null)
        {
            value = default;
            if (chars.IsEmpty)
                return false;
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            if (!TryParse(chars, bytes, separator))
                return false;
            value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
            return true;
        }

        /// <remarks>
        /// When this method returns <c>false</c>, the content of <paramref name="output"/> should
        /// be discarded. This is because bytes are written to <paramref name="output"/> as they are
        /// parsed. If parsing fails partway then <paramref name="output"/> will contain partial
        /// results and should be discarded.
        /// </remarks>
        public static bool TryParse(ReadOnlySpan<char> chars, Span<byte> output, char? separator = null)
        {
            if (chars.Length == 1 || separator is null && chars.Length % 2 != 0)
                return false;

            while (!chars.IsEmpty)
            {
                if (!byte.TryParse(chars[..2], NumberStyles.AllowHexSpecifier, null, out var b))
                    return false;
                output[0] = b;
                output = output[1..];
                chars = chars[2..];
                if (!chars.IsEmpty && separator is { } someSeparator)
                {
                    if (chars.Length < 3 || chars[0] != someSeparator)
                        return false;
                    chars = chars[1..];
                }
            }
            return true;
        }
    }
}
