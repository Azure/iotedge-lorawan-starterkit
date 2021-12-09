// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    public static class Keygen
    {
        public static string Generate(int keyLength)
        {
            var randomKey = new StringBuilder(keyLength * 2);

            for (var i = 0; i < keyLength; i++)
            {
                var newKey = RandomNumberGenerator.GetInt32(0, 256);
                _ = randomKey.Append(newKey.ToString("X2", CultureInfo.InvariantCulture));
            }

            return randomKey.ToString();
        }
    }
}
