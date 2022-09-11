// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections;

    public static class CRC16
    {
        private static BitArray shift_and_xor(BitArray array, bool input)
        {
            var shifted_array = new BitArray(array);
            // Shift entire array by 1 position to the left, 
            // filling the new empty spot with the previded input
            shifted_array = shifted_array.RightShift(1);
            shifted_array[15] = input;
            // Modify elements 3 and 10
            // They should be xor-ed with the input
            shifted_array[3] ^= input;
            shifted_array[10] ^= input;

            return shifted_array;
        }

        public static BitArray Compute(BitArray payload)
        {
            if (payload is null)
            {
                throw new ArgumentException("Provided array should be of length 16");
            }

            var remainder = new BitArray(16);
            for (var i = 0; i < payload.Length; i++)
            {
                var r = remainder[0];
                var i_xor_r = payload[i] ^ r;
                remainder = shift_and_xor(remainder, i_xor_r);
            }

            return remainder;
        }
    }
}
