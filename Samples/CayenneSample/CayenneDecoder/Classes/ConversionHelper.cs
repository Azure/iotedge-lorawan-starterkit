namespace CayenneDecoderModule.Classes
{
    using System;
    using System.Text;

    public static class ConversionHelper
    {
        /// <summary>
        /// Method enabling to convert a hex string to a byte array.
        /// </summary>
        /// <param name="hex">Input hex string</param>
        /// <returns></returns>
        public static byte[] StringToByteArray(string hex)
        {
            if (hex is null) throw new ArgumentNullException(nameof(hex));
            var NumberChars = hex.Length;
            var bytes = new byte[NumberChars / 2];
            for (var i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            if (bytes is null) throw new ArgumentNullException(nameof(bytes));
            var Result = new StringBuilder(bytes.Length * 2);
            var HexAlphabet = "0123456789ABCDEF";

            foreach (var B in bytes)
            {
                _ = Result.Append(HexAlphabet[B >> 4]);
                _ = Result.Append(HexAlphabet[B & 0xF]);
            }

            return Result.ToString();
        }
    }
}
