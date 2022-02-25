// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Security.Cryptography;

    public static class TestKeys
    {
        private static ulong GenerateKey() => (ulong)RandomNumberGenerator.GetInt32(0, int.MaxValue);

        public static NetworkSessionKey CreateNetworkSessionKey() => CreateNetworkSessionKey(GenerateKey());

        public static NetworkSessionKey CreateNetworkSessionKey(ulong value) => CreateNetworkSessionKey(0, value);

        public static NetworkSessionKey CreateNetworkSessionKey(ulong hi, ulong low)
        {
            var data128 = new Data128(hi, low);
            var buffer = new byte[Data128.Size];
            _ = data128.Write(buffer);
            return NetworkSessionKey.Read(buffer);
        }

        public static AppSessionKey CreateAppSessionKey() => CreateAppSessionKey(GenerateKey());

        public static AppSessionKey CreateAppSessionKey(ulong value) => CreateAppSessionKey(0, value);

        public static AppSessionKey CreateAppSessionKey(ulong hi, ulong low)
        {
            var data128 = new Data128(hi, low);
            var buffer = new byte[Data128.Size];
            _ = data128.Write(buffer);
            return AppSessionKey.Read(buffer);
        }

        public static AppKey CreateAppKey() => CreateAppKey(GenerateKey());

        public static AppKey CreateAppKey(ulong value) => CreateAppKey(0, value);

        public static AppKey CreateAppKey(ulong hi, ulong low)
        {
            var data128 = new Data128(hi, low);
            var buffer = new byte[Data128.Size];
            _ = data128.Write(buffer);
            return AppKey.Read(buffer);
        }
    }
}
