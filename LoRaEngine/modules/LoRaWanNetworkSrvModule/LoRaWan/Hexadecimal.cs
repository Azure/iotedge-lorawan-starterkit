// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    static class Hexadecimal
    {
        const string UpperCaseDigits = "0123456789ABCDEF";
        const string LowerCaseDigits = "0123456789abcdef";

        public static void Write(byte value, Span<char> buffer, LetterCase letterCase = LetterCase.Upper)
        {
            var digits = letterCase == LetterCase.Lower ? LowerCaseDigits : UpperCaseDigits;
            buffer[0] = digits[value >> 4];
            buffer[1] = digits[value & 0x0f];
        }

        public static void Write(ushort value, Span<char> buffer, LetterCase letterCase = LetterCase.Upper)
        {
            var digits = letterCase == LetterCase.Lower ? LowerCaseDigits : UpperCaseDigits;
            var ci = buffer.Length;
            for (var i = 0; i < 4; i++)
            {
                buffer[--ci] = digits[value & 0x000f];
                value >>= 4;
            }
        }

        public static void Write(ulong value, Span<char> buffer, LetterCase letterCase = LetterCase.Upper)
        {
            var digits = letterCase == LetterCase.Lower ? LowerCaseDigits : UpperCaseDigits;
            for (var i = 15; i >= 0; i--)
            {
                buffer[i] = digits[unchecked((int)(value & 0xfUL))];
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
        /// For an output of over 128 bytes, this method makes a heap allocation for a temporary
        /// buffer of the expected size. Otherwise all parsing induces no heap allocation.
        /// </remarks>
        public static bool TryParse(ReadOnlySpan<char> chars, Span<byte> output, char? separator = null)
        {
            static bool IsHexDigit(char ch) => ch is >= '0' and <= '9'
                                                  or >= 'A' and <= 'F'
                                                  or >= 'a' and <= 'f';

            // Fail early for the following cases:
            // - not enough source characters
            // - first or last character is not a hexadecimal digit

            if (chars.Length == 1 || !IsHexDigit(chars[0]) || !IsHexDigit(chars[^1]) || separator is null && chars.Length % 2 == 0)
                return false;

            // Create a temporary working buffer of the expected size so that we do not put partial
            // results into the final output buffer if there is a parsing error partway.

            var size = separator is null ? chars.Length / 2 : chars.Length / 3 + 1;
            var temp = size <= 128 ? stackalloc byte[size] : new byte[size];

            var i = 0;
            while (!chars.IsEmpty)
            {
                if (!byte.TryParse(chars[..2], NumberStyles.AllowHexSpecifier, null, out var b))
                    return false;

                temp[i++] = b;
                chars = chars[2..];

                if (!chars.IsEmpty && separator is { } someSeparator)
                {
                    if (chars.Length < 3 || chars[0] != someSeparator)
                        return false;
                    chars = chars[1..];
                }
            }

            if (output.Length < i)
                throw new ArgumentException(null, nameof(output));

            temp[..i].CopyTo(output);
            return true;
        }
    }
}
