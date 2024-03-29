// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Helpers
{
    using System;
    using System.Text;

    internal static class ConversionHelper
    {
        /// <summary>
        /// Method enabling to convert a hex string to a byte array.
        /// </summary>
        /// <param name="hex">Input hex string</param>
        public static byte[] StringToByteArray(string hex)
        {
            var numberChars = hex.Length;

            var bytes = new byte[numberChars >> 1];

            if (numberChars % 2 == 0)
            {
                for (var i = 0; i < numberChars; i += 2)
                    bytes[i >> 1] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }

        public static string ByteArrayToHexString(ReadOnlyMemory<byte> bytes)
        {
            var byteSpan = bytes.Span;
            var result = new StringBuilder(bytes.Length * 2);

            for (var i = 0; i < bytes.Length; i++)
            {
                _ = result.Append(Constants.HexAlphabet[byteSpan[i] >> 4])
                          .Append(Constants.HexAlphabet[byteSpan[i] & 0xF]);
            }

            return result.ToString();
        }

        public static string ReverseByteArrayToHexString(ReadOnlyMemory<byte> bytes)
        {
            var byteSpan = bytes.Span;
            var result = new StringBuilder(bytes.Length * 2);

            for (var i = bytes.Length - 1; i >= 0; --i)
            {
                _ = result.Append(Constants.HexAlphabet[byteSpan[i] >> 4])
                          .Append(Constants.HexAlphabet[byteSpan[i] & 0xF]);
            }

            return result.ToString();
        }
    }
}
