// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;
    using System.Linq;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// MIC helpers (Message Integrity Code).
    /// </summary>
    public readonly record struct Mic
    {
        public const int Size = sizeof(int);

#pragma warning disable IDE0032 // Use auto property
        private readonly int value;
#pragma warning restore IDE0032 // Use auto property

        public Mic(int value) => this.value = value;

        public override string ToString() => this.value.ToString("X4", CultureInfo.InvariantCulture);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

#pragma warning disable IDE0032 // Use auto property
        public int AsInt32 => this.value;
#pragma warning restore IDE0032 // Use auto property

        public static Mic Read(ReadOnlySpan<byte> buffer) => new(BinaryPrimitives.ReadInt32LittleEndian(buffer));

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

            return new Mic(BinaryPrimitives.ReadInt32LittleEndian(cmac));
        }

        public static Mic ComputeForJoinAccept(AppKey appKey, MacHeader macHeader, AppNonce joinNonce, NetId netId, DevAddr devAddr, ReadOnlyMemory<byte> dlSettings, RxDelay rxDelay, ReadOnlyMemory<byte> cfList)
        {
            var algoInput = new byte[MacHeader.Size + AppNonce.Size + NetId.Size + DevAddr.Size + dlSettings.Length + sizeof(RxDelay) + cfList.Length];
            var index = 0;
            _ = macHeader.Write(algoInput.AsSpan(index));
            index += MacHeader.Size;
            _ = joinNonce.Write(algoInput.AsSpan(index));
            index += AppNonce.Size;
            _ = netId.Write(algoInput.AsSpan(index));
            index += NetId.Size;
            _ = devAddr.Write(algoInput.AsSpan(index));
            index += DevAddr.Size;
            dlSettings.CopyTo(algoInput.AsMemory(index));
            index += dlSettings.Length;
            _ = rxDelay.Write(algoInput.AsSpan(index));
            index += sizeof(RxDelay);
            if (!cfList.IsEmpty)
                cfList.CopyTo(algoInput.AsMemory(index));

            var mac = MacUtilities.GetMac("AESCMAC");
            var rawKey = new byte[AppKey.Size];
            _ = appKey.Write(rawKey);
            var key = new KeyParameter(rawKey);
            mac.Init(key);
            var rfu = new byte[1];
            rfu[0] = 0x0;
            mac.BlockUpdate(algoInput, 0, algoInput.Length);
            return Read(MacUtilities.DoFinal(mac).AsSpan(0, 4));
        }

        public static Mic ComputeForData(NetworkSessionKey networkSessionKey, byte direction, DevAddr devAddr, uint fcnt, byte[] message)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            var mac = MacUtilities.GetMac("AESCMAC");
            var rawKey = new byte[NetworkSessionKey.Size];
            _ = networkSessionKey.Write(rawKey);
            mac.Init(new KeyParameter(rawKey));

            byte[] block =
            {
                0x49, 0x00, 0x00, 0x00, 0x00, direction,
                /* DevAddr */0x00, 0x00, 0x00, 0x00,
                /* FCnt */0x00, 0x00, 0x00, 0x00,
                0x00, (byte)message.Length
            };

            var pt = devAddr.Write(block.AsSpan(6));
            BinaryPrimitives.WriteUInt32LittleEndian(pt, fcnt);
            var algoinput = block.Concat(message).ToArray();

            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            return Read(MacUtilities.DoFinal(mac).AsSpan(0, 4));
        }
    }
}
