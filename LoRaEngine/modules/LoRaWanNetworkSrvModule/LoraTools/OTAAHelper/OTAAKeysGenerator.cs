// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using LoRaTools.Utils;
    using LoRaWan;

    public static class OTAAKeysGenerator
    {
        private enum SessionKeyType { Network = 1, Application = 2 }

        public static NetworkSessionKey CalculateNetworkSessionKey(byte[] appnonce, NetId netid, DevNonce devNonce, AppKey appKey)
        {
            var keyString = CalculateKey(SessionKeyType.Network, appnonce, netid, devNonce, appKey);
            return NetworkSessionKey.Parse(keyString);
        }

        public static AppSessionKey CalculateAppSessionKey(byte[] appnonce, NetId netid, DevNonce devNonce, AppKey appKey)
        {
            var keyString = CalculateKey(SessionKeyType.Application, appnonce, netid, devNonce, appKey);
            return AppSessionKey.Parse(keyString);
        }

        // don't work with CFLIST atm
        private static string CalculateKey(SessionKeyType type, byte[] appnonce, NetId netid, DevNonce devNonce, AppKey appKey)
        {
            if (appnonce is null) throw new ArgumentNullException(nameof(appnonce));

            using var aes = Aes.Create("AesManaged");
            var rawAppKey = new byte[AppKey.Size];
            _ = appKey.Write(rawAppKey);
            aes.Key = rawAppKey;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            var devNonceBytes = new byte[DevNonce.Size];
            _ = devNonce.Write(devNonceBytes);

            var buffer = new byte[1 + appnonce.Length + NetId.Size + DevNonce.Size + 7];
            var pt = buffer.AsSpan();
            Debug.Assert(pt.Length == 16);
            pt[0] = unchecked((byte)type);
            pt = pt[1..];
            appnonce.CopyTo(pt);
            pt = pt[appnonce.Length..];
            pt = netid.Write(pt);
            _ = devNonce.Write(pt);

            aes.IV = new byte[16];
            ICryptoTransform cipher;
#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            // Part of the LoRaWAN specification
            cipher = aes.CreateEncryptor();
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV

            var key = cipher.TransformFinalBlock(buffer, 0, buffer.Length);
            return ConversionHelper.ByteArrayToString(key);
        }

        public static string GetAppNonce()
        {
            Span<byte> bytes = stackalloc byte[3];
            RandomNumberGenerator.Fill(bytes);
            Span<char> chars = stackalloc char[bytes.Length * 2];
            Hexadecimal.Write(bytes, chars);
            return new string(chars);
        }
    }
}
