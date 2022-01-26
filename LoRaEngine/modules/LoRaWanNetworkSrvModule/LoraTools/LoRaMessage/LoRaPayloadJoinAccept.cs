// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using LoRaWan;
    using Org.BouncyCastle.Crypto.Engines;
    using Org.BouncyCastle.Crypto.Parameters;

    /// <summary>
    /// Implementation of a LoRa Join-Accept frame.
    /// </summary>
    public class LoRaPayloadJoinAccept : LoRaPayload
    {
        /// <summary>
        /// Gets or sets server Nonce aka JoinNonce.
        /// </summary>
        public AppNonce AppNonce { get; set; }

        /// <summary>
        /// Gets or sets device home network aka Home_NetId.
        /// </summary>
        public NetId NetId { get; set; }

        /// <summary>
        /// Gets or sets dLSettings.
        /// </summary>
        public ReadOnlyMemory<byte> DlSettings { get; set; }

        /// <summary>
        /// Gets or sets rxDelay.
        /// </summary>
        public RxDelay RxDelay { get; set; }

        /// <summary>
        /// Gets or sets cFList / Optional.
        /// </summary>
        public ReadOnlyMemory<byte> CfList { get; set; }

        public int Rx1DrOffset => (DlSettings.Span[0] >> 4) & 0b00000111;

        public DataRateIndex Rx2Dr => (DataRateIndex)(DlSettings.Span[0] & 0b00001111);

        public LoRaPayloadJoinAccept(NetId netId, DevAddr devAddr, AppNonce appNonce, byte[] dlSettings, RxDelay rxDelay, byte[] cfList)
        {
            MHdr = new MacHeader(MacMessageType.JoinAccept);
            AppNonce = appNonce;
            NetId = netId;
            DevAddr = devAddr;
            DlSettings = dlSettings.AsMemory();
            RxDelay = rxDelay;
            if (cfList is { Length: > 0 } someCfList)
                CfList = new Memory<byte>(someCfList);
        }

        public LoRaPayloadJoinAccept(ReadOnlyMemory<byte> inputMessage, AppKey appKey) : this(inputMessage.ToArray(), appKey)
        { }

        private LoRaPayloadJoinAccept(byte[] inputMessage, AppKey appKey)
        {
            if (inputMessage is null) throw new ArgumentNullException(nameof(inputMessage));

            // Only MHDR is not encrypted with the key
            // ( PHYPayload = MHDR[1] | MACPayload[..] | MIC[4] )
            MHdr = new MacHeader(inputMessage[0]);
            // Then we will take the rest and decrypt it
            // DecryptPayload(inputMessage);
            // var decrypted = PerformEncryption(appKey);
            // Array.Copy(decrypted, 0, inputMessage, 0, decrypted.Length);
            // DecryptPayload(inputMessage);
            var aesEngine = new AesEngine();
            var rawKey = new byte[AppKey.Size];
            _ = appKey.Write(rawKey);
            aesEngine.Init(true, new KeyParameter(rawKey));
            using var aes = Aes.Create("AesManaged");
            aes.Key = rawKey;
            aes.IV = new byte[16];
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            ICryptoTransform cipher;

#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            // Part of the LoRaWAN specification
            cipher = aes.CreateEncryptor();
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV
            var pt = new byte[inputMessage.Length - 1];
            Array.Copy(inputMessage, 1, pt, 0, pt.Length);
            // Array.Reverse(pt);
            var decryptedPayload = cipher.TransformFinalBlock(pt, 0, pt.Length);
            // We will copy back in the main inputMessage the content
            Array.Copy(decryptedPayload, 0, inputMessage, 1, decryptedPayload.Length);
            // ( MACPayload = AppNonce[3] | NetID[3] | DevAddr[4] | DLSettings[1] | RxDelay[1] | CFList[0|15] )
            AppNonce = AppNonce.Read(inputMessage.AsSpan(1));
            NetId = NetId.Read(inputMessage.AsSpan(4));
            DevAddr = DevAddr.Read(inputMessage.AsSpan(7));
            var dlSettings = new byte[1];
            Array.Copy(inputMessage, 11, dlSettings, 0, 1);
            DlSettings = new Memory<byte>(dlSettings);
            RxDelay = (RxDelay)(inputMessage[12] & 0b1111); // upper bits are reserved for future use
            // It's the configuration list, it can be empty or up to 15 bytes
            // - 17 = - 1 - 3 - 3 - 4 - 1 - 1 - 4
            // This is the size of all mandatory elements of the message
            var cfList = new byte[inputMessage.Length - 17];
            Array.Copy(inputMessage, 12, cfList, 0, inputMessage.Length - 17);
            Array.Reverse(cfList);
            CfList = new Memory<byte>(cfList);
            Mic = LoRaWan.Mic.Read(inputMessage.AsSpan(inputMessage.Length - 4, 4));
        }

        public byte[] Serialize(AppKey appKey)
        {
            Mic mic;
            Mic = mic = LoRaWan.Mic.ComputeForJoinAccept(appKey, MHdr, AppNonce, NetId, DevAddr, DlSettings, RxDelay, CfList);

            var channelFrequencies = !CfList.Span.IsEmpty ? CfList.ToArray() : Array.Empty<byte>();

            var buffer = new byte[AppNonce.Size + NetId.Size + DevAddr.Size + DlSettings.Length +
                                  sizeof(RxDelay) + channelFrequencies.Length + LoRaWan.Mic.Size];

            var pt = buffer.AsSpan();
            pt = AppNonce.Write(pt);
            pt = NetId.Write(pt);
            pt = DevAddr.Write(pt);
            pt = pt.Write(DlSettings.Span);
            pt = RxDelay.Write(pt);
            pt = pt.Write(channelFrequencies);
            _ = mic.Write(pt);

            using var aes = Aes.Create("AesManaged");
            var rawKey = new byte[AppKey.Size];
            _ = appKey.Write(rawKey);
            aes.Key = rawKey;
            aes.IV = new byte[16];
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            return aes.CreateDecryptor()
                      .TransformFinalBlock(buffer, 0, buffer.Length)
                      .Prepend((byte)MHdr)
                      .ToArray();
        }
    }
}
