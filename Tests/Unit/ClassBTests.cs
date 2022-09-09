// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using Xunit;

    public class ClassBTests
    {
        [Fact]
        public void CheckWorks()
        {
            byte[] payload = { 86, 0, 64 };
            byte[] remainder = new byte[2];
            var crc16 = Beacon.GenCrc16(bytes);
            Console.WriteLine(crc16);
        }
    }
}
