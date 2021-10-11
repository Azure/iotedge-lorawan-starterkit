// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;
    using System.Globalization;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    // https://www.thethingsnetwork.org/docs/lorawan/message-types/#calculating-the-message-integrity-code
    /*
        The Message Integrity Code (MIC) ensures the integrity and authenticity of a message. The message integrity code is calculated over all the fields in the message and then added to the message. The following list shows what fields are used to calculate the MIC for each message type.

        Data messages: MHDR | FHDR | FPort | FRMPayload
        Join-request messages: MHDR | JoinEUI | DevEUI | DevNonce
        Join-accept messages: MHDR | JoinNonce | NetID | DevAddr | DLSettings | RxDelay | CFList
        Rejoin-request Type 0 and 2 messages: MHDR | Rejoin Type | NetID | DevEUI | RJcount0
        Rejoin-request Type 1 messages: MHDR | Rejoin Type | JoinEUI | DevEUI | RJcount1
    */

    /// <summary>
    /// MIC helpers (Message Integrity Code).
    /// </summary>
    public readonly struct Mic
    {
        public const int Size = sizeof(uint);

        readonly uint value;

        Mic(byte a, byte b, byte c, byte d) :
            this(unchecked((uint)(a << 24 | b << 16 | c << 8 | d))) { }

        public Mic(uint value) => this.value = value;

        public bool Equals(Mic other) => this.value == other.value;
        public override bool Equals(object obj) => obj is Mic other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString("X4", CultureInfo.InvariantCulture);

        public static bool operator ==(Mic left, Mic right) => left.Equals(right);
        public static bool operator !=(Mic left, Mic right) => !left.Equals(right);

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

        public static Mic Compute(AppKey appKey, MacHeader mhdr, JoinEui joinEui, DevEui devEui, DevNonce devNonce)
        {
            var mac = MacUtilities.GetMac("AESCMAC");

            var keyBytes = new byte[AppKey.Size];
            appKey.AsUInt128.WriteLittleEndian(keyBytes);
            var key = new KeyParameter(keyBytes);
            mac.Init(key);

            var input = new byte[AppKey.Size + MacHeader.Size + JoinEui.Size + DevEui.Size + DevNonce.Size];
            var b = input.AsSpan();
            b = mhdr.Write(b);
            b = joinEui.Write(b);
            b = devEui.Write(b);
            devNonce.Write(b);

            mac.BlockUpdate(input, 0, input.Length);
            var cmac = MacUtilities.DoFinal(mac);

            return new Mic(cmac[0], cmac[1], cmac[2], cmac[3]);
        }
    }
}
