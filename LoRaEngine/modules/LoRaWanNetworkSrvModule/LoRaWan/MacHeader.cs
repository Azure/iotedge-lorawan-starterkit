// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A MAC header (MHDR) that specifies the message type (MType) and according to which major
    /// version (Major) of the frame format of the LoRaWAN layer specification the frame has been
    /// encoded.
    /// </summary>
    public readonly struct MacHeader : IEquatable<MacHeader>
    {
        public const int Size = sizeof(byte);

        readonly byte value;

        public MacHeader(byte value) => this.value = value;

        public MacMessageType MessageType => (MacMessageType)(this.value >> 5);
        public int Major => this.value & 0b11;

        public bool Equals(MacHeader other) => this.value == other.value;
        public override bool Equals(object obj) => obj is MacHeader other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => value.ToString("X2", CultureInfo.InvariantCulture);

        public static bool operator ==(MacHeader left, MacHeader right) => left.Equals(right);
        public static bool operator !=(MacHeader left, MacHeader right) => !left.Equals(right);

        public Span<byte> Write(Span<byte> buffer)
        {
            buffer[0] = this.value;
            return buffer[Size..];
        }
    }
}
