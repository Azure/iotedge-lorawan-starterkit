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
    public readonly struct Mic : IEquatable<Mic>
    {
        public const int Size = sizeof(uint);

        readonly uint value;

        public Mic(uint value) => this.value = value;

        public bool Equals(Mic other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is Mic other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString("X4", CultureInfo.InvariantCulture);

        public static bool operator ==(Mic left, Mic right) => left.Equals(right);
        public static bool operator !=(Mic left, Mic right) => !left.Equals(right);

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
            var mac = MacUtilities.GetMac("AESCMAC");

            var keyBytes = new byte[AppKey.Size];
#pragma warning disable IDE0058 // Expression value is never used
            appKey.Write(keyBytes);
#pragma warning restore IDE0058 // Expression value is never used
            var key = new KeyParameter(keyBytes);
            mac.Init(key);

            var input = new byte[MacHeader.Size + JoinEui.Size + DevEui.Size + DevNonce.Size];
            var buffer = input.AsSpan();
            buffer = mhdr.Write(buffer);
            buffer = joinEui.Write(buffer);
            buffer = devEui.Write(buffer);
#pragma warning disable IDE0058 // Expression value is never used
            devNonce.Write(buffer);
#pragma warning restore IDE0058 // Expression value is never used

            mac.BlockUpdate(input, 0, input.Length);
            var cmac = MacUtilities.DoFinal(mac);

            return new Mic(BinaryPrimitives.ReadUInt32LittleEndian(cmac));
        }
    }
}
