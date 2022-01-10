// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Security.Cryptography;
    using LoRaTools.Utils;
    using LoRaWan;

    public static class OTAAKeysGenerator
    {
        public static DevAddr GetNwkId(NetId netId)
        {
            var address = RandomNumberGenerator.GetInt32(toExclusive: DevAddr.MaxNetworkAddress + 1);
            // The 7 LBS of the NetID become the NwkID of a DevAddr:
            return new DevAddr(unchecked((byte)netId.NetworkId), address);
        }

        public static NetworkSessionKey CalculateNetworkSessionKey(byte[] type, byte[] appnonce, NetId netid, DevNonce devNonce, AppKey appKey)
        {
            var keyString = CalculateKey(type, appnonce, netid, devNonce, appKey);
            return NetworkSessionKey.Parse(keyString);
        }

        public static AppSessionKey CalculateAppSessionKey(byte[] type, byte[] appnonce, NetId netid, DevNonce devNonce, AppKey appKey)
        {
            var keyString = CalculateKey(type, appnonce, netid, devNonce, appKey);
            return AppSessionKey.Parse(keyString);
        }

        // don't work with CFLIST atm
        private static string CalculateKey(byte[] type, byte[] appnonce, NetId netId, DevNonce devNonce, AppKey appKey)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
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

            var pt = new byte[type.Length + appnonce.Length + NetId.Size + DevNonce.Size + 7];
            Array.Copy(type, pt, type.Length);
            Array.Copy(appnonce, 0, pt, type.Length, appnonce.Length);
            _ = netId.Write(pt.AsSpan(type.Length + appnonce.Length));
            _ = devNonce.Write(pt.AsSpan(type.Length + appnonce.Length + NetId.Size));

            aes.IV = new byte[16];
            ICryptoTransform cipher;
#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            // Part of the LoRaWAN specification
            cipher = aes.CreateEncryptor();
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV

            var key = cipher.TransformFinalBlock(pt, 0, pt.Length);
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
