// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Security.Cryptography;

    public static class TestEui
    {
        public static DevEui GenerateDevEui() => new DevEui((uint)RandomNumberGenerator.GetInt32(int.MaxValue));
    }
}
