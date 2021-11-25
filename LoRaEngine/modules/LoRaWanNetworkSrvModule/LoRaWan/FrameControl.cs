// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    [Flags]
#pragma warning disable CA1028 // Enum Storage should be Int32 (byte required by LoRaWAN spec)
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public enum FCtrlFlags : byte
#pragma warning restore CA1711, CA1028
    {
#pragma warning disable format
        None      = 0,
        Adr       = 0x80,
        AdrAckReq = 0x40,
        Ack       = 0x20,
        FPending  = 0x10,
#pragma warning restore format
    }

    /// <summary>
    /// Frame control octet (FCtrl) found in a frame header (FHDR) for either uplink or downlink
    /// frames.
    /// </summary>
    public readonly record struct FrameControl
    {
        private const byte FOptsLenMask = 0xf;

        public const int Size = sizeof(byte);
        private readonly byte value;

        public FrameControl(byte value) => this.value = value;

        public FrameControl(FCtrlFlags flags, int optionsLength = 0) :
            this(unchecked((byte)((byte)(((byte)flags & FOptsLenMask) == 0 ? flags : throw new ArgumentException(null, nameof(flags)))
                                         | (optionsLength is >= 0 and <= 15 ? optionsLength : throw new ArgumentOutOfRangeException(nameof(optionsLength), optionsLength, null)))))
        { }

        public static explicit operator byte(FrameControl frameControl) => frameControl.value;

        private bool HasFlags(FCtrlFlags flags) => ((FCtrlFlags)this.value & flags) == flags;

        /// <summary>
        /// Indicates whether the network will control the data rate of the end-device.
        /// Represents the ADR bit in the frame control octet (FCtrl).
        /// </summary>
        /// <remarks>
        /// If the ADR bit is set, the network will control the data rate of the end-device through
        /// the 20 appropriate MAC commands. If the ADR bit is not set, the network will not attempt
        /// to control the data rate of the end-device regardless of the received signal quality.
        /// </remarks>
        public bool Adr => HasFlags(FCtrlFlags.Adr);

        /// <summary>
        /// Indicates whether ADR acknowledgement is requested (ADRACKReq).
        /// </summary>
        public bool AdrAckRequested => HasFlags(FCtrlFlags.AdrAckReq);

        /// <summary>
        /// Indicates whether the receiver acknowledges (ACK) receiving a confirmed data message.
        /// </summary>
        public bool Ack => HasFlags(FCtrlFlags.Ack);

        /// <summary>
        /// Indicates (downlink only) whether a gateway has more data pending (FPending) to be sent.
        /// </summary>
        public bool DownlinkFramePending => HasFlags(FCtrlFlags.FPending);

        /// <summary>
        /// Gets the length of the frame options (FOpts).
        /// </summary>
        public int OptionsLength => this.value & 0x0f;

        public override string ToString() => this.value.ToString("X2", CultureInfo.InvariantCulture);

        public Span<byte> Write(Span<byte> buffer)
        {
            buffer[0] = this.value;
            return buffer[Size..];
        }
    }
}
