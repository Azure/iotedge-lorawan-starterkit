// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SensorDecoderModule.Classes
{
    using System;
    using System.Text;

    public static class ConversionHelper
    {
        /// <summary>
        /// Method enabling to convert a hex string to a byte array.
        /// </summary>
        /// <param name="hex">Input hex string</param>
        /// <returns>byte[] containing converted hex string</returns>
        public static byte[] StringToByteArray(string hex)
        {
            if (hex is null) throw new ArgumentNullException(nameof(hex));
            var numberChars = hex.Length;
            var bytes = new byte[numberChars / 2];
            for (var i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        /// <summary>
        /// Method enabling to convert a byte array to a hex string.
        /// </summary>
        /// <param name="bytes">Input byte[]</param>
        /// <returns>string containing converted byte[]</returns>
        public static string ByteArrayToString(byte[] bytes)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            var result = new StringBuilder(bytes.Length * 2);
            var hexAlphabet = "0123456789ABCDEF";

            foreach (var b in bytes)
            {
                _ = result.Append(hexAlphabet[b >> 4]);
                _ = result.Append(hexAlphabet[b & 0xF]);
            }

            return result.ToString();
        }
    }
}
