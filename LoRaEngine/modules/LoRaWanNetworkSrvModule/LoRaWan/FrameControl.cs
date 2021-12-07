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
        public const int Size = sizeof(byte);

        private const byte FOptsLenMask = 0x0f;
        private const byte FlagsMask = 0xf0;

#pragma warning disable IDE0032 // Use auto property
        private readonly byte value;
#pragma warning restore IDE0032 // Use auto property

        public FrameControl(byte value) => this.value = value;

        public FrameControl(FCtrlFlags flags, int optionsLength = 0) :
            this(unchecked((byte)((byte)(((byte)flags & FOptsLenMask) == 0 ? flags : throw new ArgumentException(null, nameof(flags)))
                                         | (optionsLength is >= 0 and <= 15 ? optionsLength : throw new ArgumentOutOfRangeException(nameof(optionsLength), optionsLength, null)))))
        { }

#pragma warning disable IDE0032 // Use auto property
        public byte AsByte => this.value;
#pragma warning restore IDE0032 // Use auto property

        public FCtrlFlags Flags => (FCtrlFlags)(this.value & FlagsMask);

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
