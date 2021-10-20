// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.Tests.Common
{
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    /// <summary>
    /// Random token generator.
    /// </summary>
    public static class RandomTokenGenerator
    {
        private static readonly RandomNumberGenerator RndKeysGenerator = new RNGCryptoServiceProvider();

        /// <summary>
        /// Gets a new token.
        /// </summary>
        public static byte[] GetToken()
        {
            var token = new byte[2];
            RndKeysGenerator.GetBytes(token);
            return token;
        }

        internal static Task<byte[]> GetTokenAsync()
        {
            var token = new byte[2];
            RndKeysGenerator.GetBytes(token);
            return Task.FromResult(token);
        }
    }
}
