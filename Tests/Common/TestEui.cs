// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Security.Cryptography;

    public static class TestEui
    {
        /// <remarks>
        /// While DevEUI are 64 bits wide, this generates a random DevEUI with only 31 LSB set.
        /// </remarks>
        public static DevEui GenerateDevEui() => new(unchecked((uint)RandomNumberGenerator.GetInt32(int.MaxValue) + 1));
    }
}
