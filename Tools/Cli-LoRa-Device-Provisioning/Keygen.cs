// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    using System;
    using System.Text;

    public static class Keygen
    {
        private static Random rnd = new Random();

        public static string Generate(int keyLength)
        {
            var randomKey = new StringBuilder(keyLength * 2);

            for (int i = 0; i < keyLength; i++)
            {
                var newKey = rnd.Next(0, 256);
                randomKey.Append(newKey.ToString("X2"));
            }

            return randomKey.ToString();
        }
    }
}
