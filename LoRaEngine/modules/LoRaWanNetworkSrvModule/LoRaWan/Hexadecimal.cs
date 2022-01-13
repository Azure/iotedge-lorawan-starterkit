// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;

    public static class Hexadecimal
    {
        private const string UpperCaseDigits = "0123456789ABCDEF";
        private const string LowerCaseDigits = "0123456789abcdef";
        private const string InsufficientBufferSizeErrorMessage = "Insufficient buffer size to encode hexadecimal.";

        private static void ValidateSufficientlySizedBuffer(int targetCharsLength, int expectedByteSize, string paramName)
        {
            if (targetCharsLength < expectedByteSize * 2)
                throw new ArgumentException(InsufficientBufferSizeErrorMessage, paramName);
        }

        public static Span<char> Write(byte value, Span<char> output, LetterCase letterCase = LetterCase.Upper)
        {
            ValidateSufficientlySizedBuffer(output.Length, sizeof(byte), nameof(output));

            var digits = letterCase == LetterCase.Lower ? LowerCaseDigits : UpperCaseDigits;
            output[0] = digits[value >> 4];
            output[1] = digits[value & 0x0f];

            return output[2..];
        }

        public static Span<char> Write(ushort value, Span<char> output, LetterCase letterCase = LetterCase.Upper)
        {
            ValidateSufficientlySizedBuffer(output.Length, sizeof(ushort), nameof(output));

            unchecked
            {
                output = Write((byte)(value >> 8), output, letterCase);
                output = Write((byte)(value >> 0), output, letterCase);
            }

            return output;
        }

        public static Span<char> Write(ulong value, Span<char> output, LetterCase letterCase = LetterCase.Upper)
        {
            ValidateSufficientlySizedBuffer(output.Length, sizeof(ulong), nameof(output));

            for (var i = sizeof(ulong) - 1; i >= 0; i--)
                output = Write(unchecked((byte)(value >> (i << 3))), output, letterCase);

            return output;
        }

        public static void Write(ReadOnlySpan<byte> buffer, Span<char> output) => Write(buffer, output, null);

        public static void Write(ReadOnlySpan<byte> buffer, Span<char> output, LetterCase letterCase) =>
            Write(buffer, output, null, letterCase);

        public static void Write(ReadOnlySpan<byte> buffer, Span<char> output, char? separator) =>
            Write(buffer, output, separator, LetterCase.Upper);

        public static void Write(ReadOnlySpan<byte> buffer, Span<char> output, char? separator, LetterCase letterCase)
        {
            var length = separator is null ? buffer.Length * 2 : (buffer.Length * 3) - 1;

            if (output.Length < length)
                throw new ArgumentException(InsufficientBufferSizeErrorMessage, nameof(output));

            for (var i = 0; i < buffer.Length; i++)
            {
                if (i > 0 && separator is { } someSeparator)
                {
                    output[0] = someSeparator;
                    output = output[1..];
                }
                output = Write(buffer[i], output, letterCase);
            }
        }

        public static bool TryParse(ReadOnlySpan<char> chars, out ulong value, char? separator = null)
        {
            value = default;
            const int size = sizeof(ulong);
            if (chars.IsEmpty || chars.Length != (separator is null ? size * 2 : (size * 3) - 1))
                return false;
            Span<byte> bytes = stackalloc byte[size];
            if (!TryParse(chars, bytes, separator))
                return false;
            value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
            return true;
        }

        public static bool TryParse(ReadOnlySpan<char> chars, out uint value, char? separator = null)
        {
            value = default;
            const int size = sizeof(uint);
            if (chars.IsEmpty || chars.Length != (separator is null ? size * 2 : (size * 3) - 1))
                return false;
            Span<byte> bytes = stackalloc byte[size];
            if (!TryParse(chars, bytes, separator))
                return false;
            value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            return true;
        }

        /// <remarks>
        /// For an output of over 128 bytes, this method makes a heap allocation for a temporary
        /// buffer of the expected size. Otherwise all parsing induces no heap allocation.
        /// </remarks>
        public static bool TryParse(ReadOnlySpan<char> chars, Span<byte> output, char? separator = null)
        {
            static bool IsHexDigit(char ch) => ch is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f');

            if (chars.IsEmpty) // nothing to do
                return true;

            // Fail early for the following cases:
            // - not enough source characters
            // - first or last character is not a hexadecimal digit

            if (chars.Length == 1 || !IsHexDigit(chars[0]) || !IsHexDigit(chars[^1]) || (separator is null && chars.Length % 2 != 0))
                return false;

            // Create a temporary working buffer of the expected size so that we do not put partial
            // results into the final output buffer if there is a parsing error partway.

            var size = separator is null ? chars.Length / 2 : (chars.Length / 3) + 1;
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
                throw new ArgumentException(InsufficientBufferSizeErrorMessage, nameof(output));

            temp[..i].CopyTo(output);
            return true;
        }
    }
}
