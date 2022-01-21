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
        private byte[] rawMessage;

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
        public Memory<byte> DlSettings { get; set; }

        /// <summary>
        /// Gets or sets rxDelay.
        /// </summary>
        public RxDelay RxDelay { get; set; }

        /// <summary>
        /// Gets or sets cFList / Optional.
        /// </summary>
        public Memory<byte> CfList { get; set; }

        public int Rx1DrOffset => (DlSettings.Span[0] >> 4) & 0b00000111;

        public DataRateIndex Rx2Dr => (DataRateIndex)(DlSettings.Span[0] & 0b00001111);

        public LoRaPayloadJoinAccept(NetId netId, DevAddr devAddr, AppNonce appNonce, byte[] dlSettings, RxDelay rxDelay, byte[] cfList)
        {
            var cfListLength = cfList == null ? 0 : cfList.Length;
            var rawMessage = this.rawMessage = new byte[1 + 12 + cfListLength];
            MHdr = new MacHeader(MacMessageType.JoinAccept);
            rawMessage[0] = (byte)MHdr;
            AppNonce = appNonce;
            _ = appNonce.Write(rawMessage.AsSpan(1));
            NetId = netId;
            _ = NetId.Write(rawMessage.AsSpan(4, 3));
            DevAddr = devAddr;
            _ = devAddr.Write(rawMessage.AsSpan(7));
            DlSettings = new Memory<byte>(rawMessage, 11, 1);
            Array.Copy(dlSettings, 0, rawMessage, 11, 1);
            RxDelay = rxDelay;
            rawMessage[12] = (byte)(Enum.IsDefined(rxDelay) ? rxDelay : default);
            // set payload Wrapper fields
            if (cfListLength > 0)
            {
                CfList = new Memory<byte>(rawMessage, 13, cfListLength);
                Array.Copy(cfList, 0, rawMessage, 13, cfListLength);
            }

            // cfList = StringToByteArray("184F84E85684B85E84886684586E8400");
            if (BitConverter.IsLittleEndian)
                DlSettings.Span.Reverse();
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
            Mic = LoRaWan.Mic.ComputeForJoinAccept(appKey, MHdr, AppNonce, NetId, DevAddr, DlSettings, RxDelay, CfList);
            _ = PerformEncryption(appKey);
            return this.rawMessage.Prepend((byte)MHdr).ToArray();
        }

        private byte[] PerformEncryption(AppKey key)
        {
            var mic = Mic ?? throw new InvalidOperationException("MIC must not be null.");

            var channelFrequencies = !CfList.Span.IsEmpty ? CfList.ToArray() : Array.Empty<byte>();

            var buffer = new byte[AppNonce.Size + NetId.Size + DevAddr.Size + DlSettings.Length +
                                  sizeof(RxDelay) + channelFrequencies.Length + LoRaWan.Mic.Size];

            static Span<byte> Copy(ReadOnlyMemory<byte> source, Span<byte> target)
            {
                source.Span.CopyTo(target);
                target = target[source.Length..];
                return target;
            }

            var pt = buffer.AsSpan();
            pt = AppNonce.Write(pt);
            pt = NetId.Write(pt);
            pt = DevAddr.Write(pt);
            pt = Copy(DlSettings, pt);
            pt = RxDelay.Write(pt);
            pt = Copy(channelFrequencies, pt);
            _ = mic.Write(pt);

            using var aes = Aes.Create("AesManaged");
            var rawKey = new byte[AppKey.Size];
            _ = key.Write(rawKey);
            aes.Key = rawKey;
            aes.IV = new byte[16];
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
            // Cipher is part of the LoRaWAN specification
            aes.Mode = CipherMode.ECB;
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
            aes.Padding = PaddingMode.None;

            ICryptoTransform cipher;

            cipher = aes.CreateDecryptor();
            var encryptedPayload = cipher.TransformFinalBlock(buffer, 0, buffer.Length);
            this.rawMessage = new byte[encryptedPayload.Length];
            Array.Copy(encryptedPayload, 0, this.rawMessage, 0, encryptedPayload.Length);
            return encryptedPayload;
        }

        public override bool CheckMic(NetworkSessionKey key, uint? server32BitFcnt = null)
        {
            throw new NotImplementedException();
        }

        public override bool CheckMic(AppKey key)
        {
            var expectedMic = LoRaWan.Mic.ComputeForJoinAccept(key, MHdr, AppNonce, NetId, DevAddr, DlSettings, RxDelay, CfList);
            return expectedMic == Mic;
        }
    }
}
