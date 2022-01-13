// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    [Flags]
#pragma warning disable CA1028 // Enum Storage should be Int32 (byte required by LoRaWAN spec)
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public enum FrameControlFlags : byte
#pragma warning restore CA1711, CA1028
    {
#pragma warning disable format
        None                  = 0,
        Adr                   = 0x80,
        AdrAckReq             = 0x40,
        Ack                   = 0x20,
#pragma warning disable CA1069 // Enums values should not be duplicated (by design)
        DownlinkFramePending  = 0x10, // FPending
        ClassB                = 0x10,
#pragma warning restore CA1069 // Enums values should not be duplicated
#pragma warning restore format
    }

    /// <summary>
    /// Frame control octet (FCtrl) found in a frame header (FHDR) for either uplink or downlink
    /// frames.
    /// </summary>
    public static class FrameControl
    {
        public const int Size = sizeof(byte);

        private const byte OptionsLengthMask = 0x0f; // FOptsLen mask
        private const byte FlagsMask = 0xf0;

        public static byte Encode(FrameControlFlags flags, int optionsLength)
        {
            var fn = ((byte)flags & OptionsLengthMask) == 0 ? (byte)flags : throw new ArgumentException(null, nameof(flags));
            var ln = optionsLength is >= 0 and <= 15 ? optionsLength : throw new ArgumentOutOfRangeException(nameof(optionsLength), optionsLength, null);
            return unchecked((byte)(fn | ln)); // legend: fn = flags nibble; ln = length nibble
        }

        public static (FrameControlFlags Flags, int OptionsLength) Decode(byte octet) =>
            ((FrameControlFlags)(octet & FlagsMask), octet & OptionsLengthMask);
    }
}
