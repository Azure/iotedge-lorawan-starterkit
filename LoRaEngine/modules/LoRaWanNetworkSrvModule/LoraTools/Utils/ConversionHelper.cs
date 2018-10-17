using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.Utils
{
    public  static class ConversionHelper
    {
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
    }
}
