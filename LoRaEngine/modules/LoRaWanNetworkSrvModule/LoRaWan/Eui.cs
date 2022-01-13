// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

    [Flags]
    public enum EuiParseOptions
    {
        None = 0,

        /// <summary>
        /// Forbids a syntactically correct DevEUI from being parsed for which validation like
        /// <see cref="DevEui.IsValid"/> will return <c>false</c>.
        /// </summary>
        ForbidInvalid = 1,
    }

    /// <summary>
    /// Global end-device ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space
    /// that uniquely identifies the end-device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For OTAA devices, the DevEUI MUST be stored in the end-device before the Join procedure
    /// is executed. ABP devices do not need the DevEUI to be stored in the device itself, but
    /// it is recommended to do so.</para>
    /// <para>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.</para>
    /// </remarks>
    public partial record struct DevEui
    {
        public bool IsValid => Eui.IsValid(this.value);

        public static bool TryParse(ReadOnlySpan<char> input, EuiParseOptions options, out DevEui result)
        {
            if (TryParse(input, out var candidate)
                && ((options & EuiParseOptions.ForbidInvalid) == EuiParseOptions.None || candidate.IsValid))
            {
                result = candidate;
                return true;
            }

            result = default;
            return false;
        }
    }

    /// <summary>
    /// Global application ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space
    /// that uniquely identifies the Join Server that is able to assist in the processing of
    /// the Join procedure and the session keys derivation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For OTAA devices, the JoinEUI MUST be stored in the end-device before the Join procedure
    /// is executed. The JoinEUI is not required for ABP only end-devices.</para>
    /// <para>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.</para>
    /// </remarks>
    public partial record struct JoinEui { }

    /// <summary>
    /// ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space that uniquely identifies
    /// a station.
    /// </summary>
    /// <remarks>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.
    /// </remarks>
    public partial record struct StationEui
    {
        public bool IsValid => Eui.IsValid(this.value);
    }

    internal static class Eui
    {
        public static string Format(ulong value, string? format)
        {
            return format switch
            {
#pragma warning disable format
                null or "G" or "N" => ToHex(value, LetterCase.Upper),
                "d"                => ToHex(value, '-', LetterCase.Lower),
                "D"                => ToHex(value, '-', LetterCase.Upper),
                "E"                => ToHex(value, ':', LetterCase.Upper),
                "e"                => ToHex(value, ':', LetterCase.Lower),
                "n" or "g"         => ToHex(value, LetterCase.Lower),
                "I"                => Id6.Format(value, Id6.FormatOptions.FixedWidth),
                "i"                => Id6.Format(value, Id6.FormatOptions.FixedWidth | Id6.FormatOptions.Lowercase),
#pragma warning restore format
                _ => throw new FormatException(@"Format string can only be null, ""G"", ""g"", ""D"", ""d"", ""I"", ""i"", ""N"", ""n"", ""E"" or ""e"".")
            };
        }

        public static string ToHex(ulong value) => ToHex(value, null);
        public static string ToHex(ulong value, LetterCase letterCase) => ToHex(value, null, letterCase);
        public static string ToHex(ulong value, char? separator) => ToHex(value, separator, LetterCase.Upper);

        public static string ToHex(ulong value, char? separator, LetterCase letterCase)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
            var length = separator is null ? bytes.Length * 2 : (bytes.Length * 3) - 1;
            var chars = length <= 128 ? stackalloc char[length] : new char[length];
            Hexadecimal.Write(bytes, chars, separator, letterCase);
            return new string(chars);
        }

        public static bool TryParse(ReadOnlySpan<char> input, out ulong result) =>
            input.Length switch
            {
                23 => Hexadecimal.TryParse(input, out result, '-')  // e.g. "88:99:AA:BB:CC:DD:EE:FF"
                   || Hexadecimal.TryParse(input, out result, ':'), // e.g. "88-99-AA-BB-CC-DD-EE-FF"
                16 => Hexadecimal.TryParse(input, out result),      // e.g. "8899AABBCCDDEEFF"
                _ => Id6.TryParse(input, out result)                // e.g. "8899:AABB:CCDD:EEFF"
            };

        public static bool IsValid(ulong value) => value is not 0 and not 0xffff_ffff_ffff_ffff;
    }
}
