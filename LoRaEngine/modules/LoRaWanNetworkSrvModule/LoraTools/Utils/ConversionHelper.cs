using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.Utils
{
    public  static class ConversionHelper
    {
        const string HexAlphabet = "0123456789ABCDEF";

        /// <summary>
        /// Method enabling to convert a hex string to a byte array.
        /// </summary>
        /// <param name="hex">Input hex string</param>
        /// <returns></returns>
        public static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static string ByteArrayToString(ReadOnlyMemory<byte> bytes)
        {
            var byteSpan = bytes.Span;
            var result = new StringBuilder(bytes.Length * 2);
            

            for (var i=0; i < bytes.Length; i++)
            {
                result.Append(HexAlphabet[(int)(byteSpan[i] >> 4)]);
                result.Append(HexAlphabet[(int)(byteSpan[i] & 0xF)]);
            }

            return result.ToString();
        }

        static string ByteArrayToString(byte[] bytes)
        {
            StringBuilder Result = new StringBuilder(bytes.Length * 2);

            foreach (byte B in bytes)
            {
                Result.Append(HexAlphabet[(int)(B >> 4)]);
                Result.Append(HexAlphabet[(int)(B & 0xF)]);
            }

            return Result.ToString();
        }
    }
}
