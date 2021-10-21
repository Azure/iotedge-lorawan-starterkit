// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Utils
{
    using System;
    using System.Text;

    public static class ConversionHelper
    {
        private const string HexAlphabet = "0123456789ABCDEF";

        /// <summary>
        /// Method enabling to convert a hex string to a byte array.
        /// </summary>
        /// <param name="hex">Input hex string.</param>
        public static byte[] StringToByteArray(string hex)
        {
            if (hex is null) throw new ArgumentNullException(nameof(hex));

            var numberChars = hex.Length;
            var bytes = new byte[numberChars / 2];
            for (var i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static string ByteArrayToString(ReadOnlyMemory<byte> bytes)
        {
            var byteSpan = bytes.Span;
            var result = new StringBuilder(bytes.Length * 2);

            for (var i = 0; i < bytes.Length; i++)
            {
                _ = result.Append(HexAlphabet[byteSpan[i] >> 4]);
                _ = result.Append(HexAlphabet[byteSpan[i] & 0xF]);
            }

            return result.ToString();
        }

        public static string ReverseByteArrayToString(ReadOnlyMemory<byte> bytes)
        {
            var byteSpan = bytes.Span;
            var result = new StringBuilder(bytes.Length * 2);

            for (var i = bytes.Length - 1; i >= 0; --i)
            {
                _ = result.Append(HexAlphabet[byteSpan[i] >> 4]);
                _ = result.Append(HexAlphabet[byteSpan[i] & 0xF]);
            }

            return result.ToString();
        }
    }
}
