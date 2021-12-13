// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using LoRaTools.Utils;
    using LoRaWan;

    public static class OTAAKeysGenerator
    {
        private static readonly RandomNumberGenerator RndKeysGenerator = RandomNumberGenerator.Create();

        public static string GetNwkId(byte[] netId)
        {
            if (netId is null) throw new ArgumentNullException(nameof(netId));

            var nwkPart = netId[0] << 1;
            var devAddr = new byte[4];

            RndKeysGenerator.GetBytes(devAddr);

            devAddr[0] = (byte)((nwkPart & 0b11111110) | (devAddr[0] & 0b00000001));
            return ConversionHelper.ByteArrayToString(devAddr);
        }

        // don't work with CFLIST atm
        public static string CalculateKey(byte[] type, byte[] appnonce, byte[] netid, DevNonce devnonce, byte[] appKey)
        {
            using var aes = Aes.Create("AesManaged");
            aes.Key = appKey;
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            var devNonceBytes = new byte[DevNonce.Size];
            _ = devnonce.Write(devNonceBytes);

            var pt = type.Concat(appnonce).Concat(netid).Concat(devNonceBytes).Concat(new byte[7]).ToArray();

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
            var appNonce = new byte[3];
            RndKeysGenerator.GetBytes(appNonce);
            return ConversionHelper.ByteArrayToString(appNonce);
        }
    }
}
