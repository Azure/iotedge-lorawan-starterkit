// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class Beacon
    {

        public static byte[] GenerateFrames()
        {
            var message = new byte[8];
            // RFU section
            message[0] = 0x0;
            message[1] = 0x0;

            // time sectionu
            var epochTime = (long)(DateTime.UtcNow -
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds % (2 ^ 32);
            BitConverter.GetBytes(epochTime).CopyTo(message, 2);

            // crc section
            var crc = GenCrc16(message[0..6]);

            return message;
        }


        /// <summary>
        /// Gens the CRC16.
        /// CRC-1021 = X(16)+x(12)+x(5)+1
        /// </summary>
        /// <param name="c">The c.</param>
        /// <param name="nByte">The n byte.</param>
        /// <returns>System.Byte[][].</returns>
        public static ushort GenCrc16(byte[] c)
        {
            uint poly = 69665;
            this.initialValue = (ushort)initialValue;
            ushort temp, a;
            for (int i = 0; i < table.Length; ++i)
            {
                temp = 0;
                a = (ushort)(i << 8);
                for (int j = 0; j < 8; ++j)
                {
                    if (((temp ^ a) & 0x8000) != 0)
                    {
                        temp = (ushort)((temp << 1) ^ poly);
                    }
                    else
                    {
                        temp <<= 1;
                    }
                    a <<= 1;
                }
                table[i] = temp;
            }
        }

        public Crc16(Crc16Mode mode)
        {
            uint polynomial = 69665;
            ushort value;
            ushort temp;
            for (ushort i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (ushort)((value >> 1) ^ polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                table[i] = value;
            }
        }
}
