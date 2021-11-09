// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

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
    public partial struct DevEui { }

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
    public partial struct JoinEui { }

    /// <summary>
    /// ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space that uniquely identifies
    /// a station.
    /// </summary>
    /// <remarks>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.
    /// </remarks>
    public partial struct StationEui { }

    internal static class Eui
    {
        public static string Format(ulong value, string? format)
        {
            return format switch
            {
                null or "G" or "D" => ToHex(value, '-', LetterCase.Upper),
                "g" or "d"         => ToHex(value, '-', LetterCase.Lower),
                "E"                => ToHex(value, ':', LetterCase.Upper),
                "e"                => ToHex(value, ':', LetterCase.Lower),
                "N"                => ToHex(value, null, LetterCase.Upper),
                "n"                => ToHex(value, null, LetterCase.Lower),
                "I"                => Id6.Format(value, Id6.FormatOptions.FixedWidth),
                "i"                => Id6.Format(value, Id6.FormatOptions.FixedWidth | Id6.FormatOptions.Lowercase),
                _ => throw new FormatException(@"Format string can only be null, ""G"", ""g"", ""D"", ""d"", ""I"", ""i"", ""N"", ""n"", ""E"" or ""e"".")
            };

            static string ToHex(ulong value, char? separator, LetterCase letterCase)
            {
                Span<byte> bytes = stackalloc byte[sizeof(ulong)];
                BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
                var nChars = separator is null ? bytes.Length * 2 : (bytes.Length * 3) - 1;
                Span<char> chars = stackalloc char[nChars];
                Hexadecimal.Write(bytes, chars, separator, letterCase);
                return new string(chars);
            }
        }
    }
}
