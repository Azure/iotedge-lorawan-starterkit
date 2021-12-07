// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;

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

        private readonly FCtrlFlags flags;
        private readonly byte optionsLength;

        public FrameControl(byte value) : this((FCtrlFlags)(value & FlagsMask), value & FOptsLenMask) { }

        public FrameControl(FCtrlFlags flags, int optionsLength = 0)
        {
            this.flags = ValidateArg(flags);
            this.optionsLength = ValidateOptionsLengthArg(optionsLength);
        }

        private static FCtrlFlags ValidateArg(FCtrlFlags arg, [CallerArgumentExpression("arg")] string? paramName = null) =>
            ((byte)arg & FOptsLenMask) == 0 ? arg : throw new ArgumentException(null, paramName);

        private static byte ValidateOptionsLengthArg(int arg, [CallerArgumentExpression("arg")] string? paramName = null) =>
            arg is >= 0 and <= 15 ? unchecked((byte)arg) : throw new ArgumentOutOfRangeException(paramName, arg, null);

        public FCtrlFlags Flags
        {
            get => this.flags;
            init => this.flags = ValidateArg(value);
        }

        private bool HasFlags(FCtrlFlags flags) => (this.flags & flags) == flags;
        private FCtrlFlags With(FCtrlFlags flags, bool value) => value ? this.flags | flags : this.flags & ~flags;

        /// <summary>
        /// Indicates whether the network will control the data rate of the end-device.
        /// Represents the ADR bit in the frame control octet (FCtrl).
        /// </summary>
        /// <remarks>
        /// If the ADR bit is set, the network will control the data rate of the end-device through
        /// the 20 appropriate MAC commands. If the ADR bit is not set, the network will not attempt
        /// to control the data rate of the end-device regardless of the received signal quality.
        /// </remarks>
        public bool Adr
        {
            get => HasFlags(FCtrlFlags.Adr);
            init => this.flags = With(FCtrlFlags.Adr, value);
        }

        /// <summary>
        /// Indicates whether ADR acknowledgement is requested (ADRACKReq).
        /// </summary>
        public bool AdrAckRequested
        {
            get => HasFlags(FCtrlFlags.AdrAckReq);
            init => this.flags = With(FCtrlFlags.AdrAckReq, value);
        }

        /// <summary>
        /// Indicates whether the receiver acknowledges (ACK) receiving a confirmed data message.
        /// </summary>
        public bool Ack
        {
            get => HasFlags(FCtrlFlags.Ack);
            init => this.flags = With(FCtrlFlags.Ack, value);
        }

        /// <summary>
        /// Indicates (downlink only) whether a gateway has more data pending (FPending) to be sent.
        /// </summary>
        public bool DownlinkFramePending
        {
            get => HasFlags(FCtrlFlags.FPending);
            init => this.flags = With(FCtrlFlags.FPending, value);
        }

        /// <summary>
        /// Gets the length of the frame options (FOpts).
        /// </summary>
        public int OptionsLength
        {
            get => this.optionsLength;
            init => this.optionsLength = ValidateOptionsLengthArg(value);
        }

        public override string ToString() => ((byte)this).ToString("X2", CultureInfo.InvariantCulture);

        public Span<byte> Write(Span<byte> buffer)
        {
            buffer[0] = (byte)this;
            return buffer[Size..];
        }

        public static explicit operator byte(FrameControl frameControl) =>
            (byte)((byte)frameControl.Flags | frameControl.OptionsLength);

        public static implicit operator FCtrlFlags(FrameControl frameControl) => frameControl.Flags;
    }
}
