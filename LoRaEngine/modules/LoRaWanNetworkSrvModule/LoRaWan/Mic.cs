// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// MIC helpers (Message Integrity Code).
    /// </summary>
    public readonly record struct Mic
    {
        public const int Size = sizeof(uint);

        private readonly uint value;

        public Mic(uint value) => this.value = value;

        public override string ToString() => this.value.ToString("X4", CultureInfo.InvariantCulture);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

        public static Mic Read(ReadOnlySpan<byte> buffer) => new(BinaryPrimitives.ReadUInt32LittleEndian(buffer));

        //   The Message Integrity Code (MIC) ensures the integrity and authenticity of a message.
        //   The message integrity code is calculated over all the fields in the message and then added
        //   to the message. The following list shows what fields are used to calculate the MIC for each
        //   message type.
        //
        //   - Data messages: MHDR | FHDR | FPort | FRMPayload
        //   - Join-request messages: MHDR | JoinEUI | DevEUI | DevNonce
        //   - Join-accept messages: MHDR | JoinNonce | NetID | DevAddr | DLSettings | RxDelay | CFList
        //   - Rejoin-request Type 0 and 2 messages: MHDR | Rejoin Type | NetID | DevEUI | RJcount0
        //   - Rejoin-request Type 1 messages: MHDR | Rejoin Type | JoinEUI | DevEUI | RJcount1
        //
        // Source: https://www.thethingsnetwork.org/docs/lorawan/message-types/#calculating-the-message-integrity-code

        public static Mic ComputeForJoinRequest(AppKey appKey, MacHeader mhdr, JoinEui joinEui, DevEui devEui, DevNonce devNonce)
        {
            var keyBytes = new byte[AppKey.Size];
            _ = appKey.Write(keyBytes);
            return ComputeForJoinRequest(keyBytes, mhdr, joinEui, devEui, devNonce);
        }

        public static Mic ComputeForJoinRequest(NetworkSessionKey networkSessionKey, MacHeader mhdr, JoinEui joinEui, DevEui devEui, DevNonce devNonce)
        {
            var keyBytes = new byte[NetworkSessionKey.Size];
            _ = networkSessionKey.Write(keyBytes);
            return ComputeForJoinRequest(keyBytes, mhdr, joinEui, devEui, devNonce);
        }

        private static Mic ComputeForJoinRequest(byte[] keyBytes, MacHeader mhdr, JoinEui joinEui, DevEui devEui, DevNonce devNonce)
        {
            var mac = MacUtilities.GetMac("AESCMAC");

            var key = new KeyParameter(keyBytes);
            mac.Init(key);

            var input = new byte[MacHeader.Size + JoinEui.Size + DevEui.Size + DevNonce.Size];
            var buffer = input.AsSpan();
            buffer = mhdr.Write(buffer);
            buffer = joinEui.Write(buffer);
            buffer = devEui.Write(buffer);
            _ = devNonce.Write(buffer);

            mac.BlockUpdate(input, 0, input.Length);
            var cmac = MacUtilities.DoFinal(mac);

            return new Mic(BinaryPrimitives.ReadUInt32LittleEndian(cmac));
        }
    }
}
