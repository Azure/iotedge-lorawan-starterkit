// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using LoRaWan;

    public static class OTAAKeysGenerator
    {
        private enum SessionKeyType { Network = 1, Application = 2 }

        public static NetworkSessionKey CalculateNetworkSessionKey(AppNonce appNonce, NetId netId, DevNonce devNonce, AppKey appKey) =>
            NetworkSessionKey.Read(CalculateKey(SessionKeyType.Network, appNonce, netId, devNonce, appKey));

        public static AppSessionKey CalculateAppSessionKey(AppNonce appNonce, NetId netId, DevNonce devNonce, AppKey appKey) =>
            AppSessionKey.Read(CalculateKey(SessionKeyType.Application, appNonce, netId, devNonce, appKey));

        // don't work with CFLIST atm
        private static byte[] CalculateKey(SessionKeyType type, AppNonce appNonce, NetId netId, DevNonce devNonce, AppKey appKey)
        {
            using var aes = Aes.Create("AesManaged");
            var rawAppKey = new byte[AppKey.Size];
            _ = appKey.Write(rawAppKey);
            aes.Key = rawAppKey;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            var buffer = new byte[1 + AppNonce.Size + NetId.Size + DevNonce.Size + 7];
            var pt = buffer.AsSpan();
            Debug.Assert(pt.Length == 16);
            pt[0] = unchecked((byte)type);
            pt = pt[1..];
            pt = appNonce.Write(pt);
            pt = netId.Write(pt);
            pt = devNonce.Write(pt);
            Debug.Assert(pt.Length == 7);

            aes.IV = new byte[16];
            ICryptoTransform cipher;
#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            // Part of the LoRaWAN specification
            cipher = aes.CreateEncryptor();
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV

            return cipher.TransformFinalBlock(buffer, 0, buffer.Length);
        }
    }
}
