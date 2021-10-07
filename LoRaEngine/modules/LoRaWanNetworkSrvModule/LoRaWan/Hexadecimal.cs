// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    static class Hexadecimal
    {
        const string Digits = "0123456789abcdef";

        public static void Write(ulong value, Span<char> buffer)
        {
            var ci = buffer.Length;
            for (var i = 0; i < 16; i++)
            {
                buffer[--ci] = Digits[(int)(value & 0x0000000f)];
                value >>= 4;
            }
        }
    }
}
