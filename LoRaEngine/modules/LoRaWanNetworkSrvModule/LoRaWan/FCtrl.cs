// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    [Flags]
#pragma warning disable CA1028 // Enum Storage should be Int32
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
#pragma warning disable CA2217 // Do not mark enums with FlagsAttribute
    public enum FCtrlFlags : byte
#pragma warning restore CA2217, CA1711, CA1028
    {
        Adr          = 0x80,
        AdrAckReq    = 0x40,
        Ack          = 0x20,
        FPending     = 0x10,
        FOptsLenMask = 0x0f,
    }

    /// <summary>
    /// Frame control octet (FCtrl) found in a frame header (FHDR) for either uplink or downlink frames.
    /// </summary>
    public readonly struct FCtrl : IEquatable<FCtrl>
    {
        readonly byte value;

        public FCtrl(byte value) => this.value = value;

        public FCtrl(FCtrlFlags flags, int fOptsLen) :
            this(unchecked((byte)((byte)((flags & FCtrlFlags.FOptsLenMask) == 0 ? flags : throw new ArgumentException(null, nameof(flags)))
                                         | (fOptsLen is >= 0 and <= 15 ? fOptsLen : throw new ArgumentOutOfRangeException(nameof(fOptsLen), fOptsLen, null))))) { }

        bool HasFlags(FCtrlFlags flags) => ((FCtrlFlags)this.value & flags) == flags;

        /// <summary>
        /// Indicates whether the network will control the data rate of the end-device.
        /// </summary>
        /// <remarks>
        /// Represents the ADR bit in the frame control octet (FCtrl). If the ADR bit is set, the
        /// network will control the data rate of the end-device through the 20 appropriate MAC
        /// commands. If the ADR bit is not set, the network will not attempt to control the data
        /// rate of the end-device regardless of the received signal quality.
        /// </remarks>
        public bool Adr => HasFlags(FCtrlFlags.Adr);

        /// <summary>
        /// Indicates whether ADR acknowledgement is requested.
        /// </summary>
        public bool AdrAckReq => HasFlags(FCtrlFlags.AdrAckReq);

        /// <summary>
        /// Indicates whether the receiver acknowledges receiving a confirmed data message.
        /// </summary>
        public bool Ack => HasFlags(FCtrlFlags.Ack);

        /// <summary>
        /// Indicates (downlink only) whether a gateway has more data pending to be sent.
        /// </summary>
        public bool FPending => HasFlags(FCtrlFlags.FPending);

        /// <summary>
        /// Gets the length of the frame options (FOpts).
        /// </summary>
        public int FOptsLen => this.value & 0x0f;

        public bool Equals(FCtrl other) => this.value == other.value;
        public override bool Equals(object obj) => obj is FCtrl other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => value.ToString("X2", CultureInfo.InvariantCulture);

        public static bool operator ==(FCtrl left, FCtrl right) => left.Equals(right);
        public static bool operator !=(FCtrl left, FCtrl right) => !left.Equals(right);
    }
}
