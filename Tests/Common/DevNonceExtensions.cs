// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    public static class DevNonceExtensions
    {
        public static byte[] GetBytes(this DevNonce devNonce)
        {
            var bytes = new byte[DevNonce.Size];
            _ = devNonce.Write(bytes);
            return bytes;
        }
    }
}
