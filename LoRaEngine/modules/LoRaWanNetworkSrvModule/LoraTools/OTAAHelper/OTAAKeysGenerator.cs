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
        // Those fields are constants defined in the specs.
        private const int NwkKeyCalculationType = 0x01;
        private const int AppKeyCalculationType = 0x02;
        public static DevAddr GetNwkId(uint netId)
        {
            var address = RandomNumberGenerator.GetInt32(toExclusive: DevAddr.MaxNetworkAddress + 1);
            // The 7 LBS of the NetID become the NwkID of a DevAddr:
            return new DevAddr(unchecked((byte)netId) & 0b0111_1111, address);
        }

        public static NetworkSessionKey CalculateNetworkSessionKey(byte[] appnonce, byte[] netid, DevNonce devNonce, AppKey appKey)
        {
            var keyString = CalculateKey(new byte[1] { NwkKeyCalculationType }, appnonce, netid, devNonce, appKey);
            return NetworkSessionKey.Parse(keyString);
        }

        public static AppSessionKey CalculateAppSessionKey(byte[] appnonce, byte[] netid, DevNonce devNonce, AppKey appKey)
        {
            var keyString = CalculateKey(new byte[1] { AppKeyCalculationType }, appnonce, netid, devNonce, appKey);
            return AppSessionKey.Parse(keyString);
        }

        // don't work with CFLIST atm
        private static string CalculateKey(byte[] type, byte[] appnonce, byte[] netid, DevNonce devNonce, AppKey appKey)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            if (appnonce is null) throw new ArgumentNullException(nameof(appnonce));
            if (netid is null) throw new ArgumentNullException(nameof(netid));

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

            var pt = new byte[type.Length + appnonce.Length + netid.Length + DevNonce.Size + 7];
            Array.Copy(type, pt, type.Length);
            Array.Copy(appnonce, 0, pt, type.Length, appnonce.Length);
            Array.Copy(netid, 0, pt, type.Length + appnonce.Length, netid.Length);
            _ = devNonce.Write(pt.AsSpan(type.Length + appnonce.Length + netid.Length));

            aes.IV = new byte[16];
            ICryptoTransform cipher;
#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            // Part of the LoRaWAN specification
            cipher = aes.CreateEncryptor();
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV

            var key = cipher.TransformFinalBlock(pt, 0, pt.Length);
            return ConversionHelper.ByteArrayToString(key);
        }

        // don't work with CFLIST atm
        public static string CalculateKey(byte[] type, byte[] appnonce, byte[] netid, ReadOnlyMemory<byte> devnonce, byte[] appKey)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            if (appnonce is null) throw new ArgumentNullException(nameof(appnonce));
            if (netid is null) throw new ArgumentNullException(nameof(netid));

            using var aes = Aes.Create("AesManaged");
            aes.Key = appKey;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            var pt = new byte[type.Length + appnonce.Length + netid.Length + devnonce.Length + 7];
            var destIndex = 0;
            Array.Copy(type, 0, pt, destIndex, type.Length);

            destIndex += type.Length;
            Array.Copy(appnonce, 0, pt, destIndex, appnonce.Length);

            destIndex += appnonce.Length;
            Array.Copy(netid, 0, pt, destIndex, netid.Length);

            destIndex += netid.Length;
            devnonce.CopyTo(new Memory<byte>(pt, destIndex, devnonce.Length));

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
