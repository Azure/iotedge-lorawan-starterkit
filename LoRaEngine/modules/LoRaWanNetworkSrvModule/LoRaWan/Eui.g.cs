// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//------------------------------------------------------------------------------
// This code was generated by a tool.
// Changes to this file will be lost if the code is re-generated.
//------------------------------------------------------------------------------

#nullable enable

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

    readonly partial struct DevEui : IEquatable<DevEui>, IFormattable
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public DevEui(ulong value) => this.value = value;

        public ulong AsUInt64 => this.value;

        public bool Equals(DevEui other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is DevEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => ToString("G", null);

        public static bool operator ==(DevEui left, DevEui right) => left.Equals(right);
        public static bool operator !=(DevEui left, DevEui right) => !left.Equals(right);

        public static DevEui Read(ReadOnlySpan<byte> buffer) =>
            new(BinaryPrimitives.ReadUInt64LittleEndian(buffer));

        public static DevEui Read(ref ReadOnlySpan<byte> buffer)
        {
            var result = Read(buffer);
            buffer = buffer[Size..];
            return result;
        }

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

        public static DevEui Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static DevEui Parse(ReadOnlySpan<char> input, string? format) =>
            TryParse(input, format, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out DevEui result) => TryParse(input, "G", out result);

        public static bool TryParse(ReadOnlySpan<char> input, string? format, out DevEui result)
        {
            result = default;

            return format?.ToLowerInvariant() switch
            {
                null or "g" or "d"  => TryParseHex(input, '-', out result),
                "e"                 => TryParseHex(input, ':', out result),
                "n"                 => TryParseHex(input, null, out result),
                "i"                 => TryParseId6(input, out result),
                _                   => false
            };

            static bool TryParseHex(ReadOnlySpan<char> input, char? separator, out DevEui result)
            {
                if (Hexadecimal.TryParse(input, out var raw, separator))
                {
                    result = new DevEui(raw);
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }

            static bool TryParseId6(ReadOnlySpan<char> input, out DevEui result)
            {
                if (Id6.TryParse(input, out var raw))
                {
                    result = new DevEui(raw);
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            return format switch
            {
                null or "G" or "D"  => ToHex(this.value, '-', LetterCase.Upper),
                "g" or "d"          => ToHex(this.value, '-', LetterCase.Lower),
                "E"                 => ToHex(this.value, ':', LetterCase.Upper),
                "e"                 => ToHex(this.value, ':', LetterCase.Lower),
                "N"                 => ToHex(this.value, null, LetterCase.Upper),
                "n"                 => ToHex(this.value, null, LetterCase.Lower),
                "I"                 => Id6.Format(this.value, Id6.FormatOptions.FixedWidth),
                "i"                 => Id6.Format(this.value, Id6.FormatOptions.FixedWidth | Id6.FormatOptions.Lowercase),
                _ => throw new FormatException(@"Format string can only be null, ""G"", ""g"", ""D"", ""d"", ""I"", ""i"", ""N"", ""n"", ""E"" or ""e"".")
            };

            static string ToHex(ulong value, char? separator, LetterCase letterCase)
            {
                Span<byte> bytes = stackalloc byte[sizeof(ulong)];
                var nChars = separator is null ? bytes.Length * 2 : bytes.Length * 3 - 1;
                Span<char> chars = stackalloc char[nChars];
                BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
                Hexadecimal.Write(bytes, chars, separator, letterCase);
                return new string(chars);
            }
        }
    }

    readonly partial struct JoinEui : IEquatable<JoinEui>, IFormattable
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public JoinEui(ulong value) => this.value = value;

        public ulong AsUInt64 => this.value;

        public bool Equals(JoinEui other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is JoinEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => ToString("G", null);

        public static bool operator ==(JoinEui left, JoinEui right) => left.Equals(right);
        public static bool operator !=(JoinEui left, JoinEui right) => !left.Equals(right);

        public static JoinEui Read(ReadOnlySpan<byte> buffer) =>
            new(BinaryPrimitives.ReadUInt64LittleEndian(buffer));

        public static JoinEui Read(ref ReadOnlySpan<byte> buffer)
        {
            var result = Read(buffer);
            buffer = buffer[Size..];
            return result;
        }

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

        public static JoinEui Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static JoinEui Parse(ReadOnlySpan<char> input, string? format) =>
            TryParse(input, format, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out JoinEui result) => TryParse(input, "G", out result);

        public static bool TryParse(ReadOnlySpan<char> input, string? format, out JoinEui result)
        {
            result = default;

            return format?.ToLowerInvariant() switch
            {
                null or "g" or "d"  => TryParseHex(input, '-', out result),
                "e"                 => TryParseHex(input, ':', out result),
                "n"                 => TryParseHex(input, null, out result),
                "i"                 => TryParseId6(input, out result),
                _                   => false
            };

            static bool TryParseHex(ReadOnlySpan<char> input, char? separator, out JoinEui result)
            {
                if (Hexadecimal.TryParse(input, out var raw, separator))
                {
                    result = new JoinEui(raw);
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }

            static bool TryParseId6(ReadOnlySpan<char> input, out JoinEui result)
            {
                if (Id6.TryParse(input, out var raw))
                {
                    result = new JoinEui(raw);
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            return format switch
            {
                null or "G" or "D"  => ToHex(this.value, '-', LetterCase.Upper),
                "g" or "d"          => ToHex(this.value, '-', LetterCase.Lower),
                "E"                 => ToHex(this.value, ':', LetterCase.Upper),
                "e"                 => ToHex(this.value, ':', LetterCase.Lower),
                "N"                 => ToHex(this.value, null, LetterCase.Upper),
                "n"                 => ToHex(this.value, null, LetterCase.Lower),
                "I"                 => Id6.Format(this.value, Id6.FormatOptions.FixedWidth),
                "i"                 => Id6.Format(this.value, Id6.FormatOptions.FixedWidth | Id6.FormatOptions.Lowercase),
                _ => throw new FormatException(@"Format string can only be null, ""G"", ""g"", ""D"", ""d"", ""I"", ""i"", ""N"", ""n"", ""E"" or ""e"".")
            };

            static string ToHex(ulong value, char? separator, LetterCase letterCase)
            {
                Span<byte> bytes = stackalloc byte[sizeof(ulong)];
                var nChars = separator is null ? bytes.Length * 2 : bytes.Length * 3 - 1;
                Span<char> chars = stackalloc char[nChars];
                BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
                Hexadecimal.Write(bytes, chars, separator, letterCase);
                return new string(chars);
            }
        }
    }

    readonly partial struct StationEui : IEquatable<StationEui>, IFormattable
    {
        public const int Size = sizeof(ulong);

        readonly ulong value;

        public StationEui(ulong value) => this.value = value;

        public ulong AsUInt64 => this.value;

        public bool Equals(StationEui other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is StationEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => ToString("G", null);

        public static bool operator ==(StationEui left, StationEui right) => left.Equals(right);
        public static bool operator !=(StationEui left, StationEui right) => !left.Equals(right);

        public static StationEui Read(ReadOnlySpan<byte> buffer) =>
            new(BinaryPrimitives.ReadUInt64LittleEndian(buffer));

        public static StationEui Read(ref ReadOnlySpan<byte> buffer)
        {
            var result = Read(buffer);
            buffer = buffer[Size..];
            return result;
        }

        public Span<byte> Write(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, this.value);
            return buffer[Size..];
        }

        public static StationEui Parse(ReadOnlySpan<char> input) =>
            TryParse(input, out var result) ? result : throw new FormatException();

        public static StationEui Parse(ReadOnlySpan<char> input, string? format) =>
            TryParse(input, format, out var result) ? result : throw new FormatException();

        public static bool TryParse(ReadOnlySpan<char> input, out StationEui result) => TryParse(input, "G", out result);

        public static bool TryParse(ReadOnlySpan<char> input, string? format, out StationEui result)
        {
            result = default;

            return format?.ToLowerInvariant() switch
            {
                null or "g" or "d"  => TryParseHex(input, '-', out result),
                "e"                 => TryParseHex(input, ':', out result),
                "n"                 => TryParseHex(input, null, out result),
                "i"                 => TryParseId6(input, out result),
                _                   => false
            };

            static bool TryParseHex(ReadOnlySpan<char> input, char? separator, out StationEui result)
            {
                if (Hexadecimal.TryParse(input, out var raw, separator))
                {
                    result = new StationEui(raw);
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }

            static bool TryParseId6(ReadOnlySpan<char> input, out StationEui result)
            {
                if (Id6.TryParse(input, out var raw))
                {
                    result = new StationEui(raw);
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            return format switch
            {
                null or "G" or "D"  => ToHex(this.value, '-', LetterCase.Upper),
                "g" or "d"          => ToHex(this.value, '-', LetterCase.Lower),
                "E"                 => ToHex(this.value, ':', LetterCase.Upper),
                "e"                 => ToHex(this.value, ':', LetterCase.Lower),
                "N"                 => ToHex(this.value, null, LetterCase.Upper),
                "n"                 => ToHex(this.value, null, LetterCase.Lower),
                "I"                 => Id6.Format(this.value, Id6.FormatOptions.FixedWidth),
                "i"                 => Id6.Format(this.value, Id6.FormatOptions.FixedWidth | Id6.FormatOptions.Lowercase),
                _ => throw new FormatException(@"Format string can only be null, ""G"", ""g"", ""D"", ""d"", ""I"", ""i"", ""N"", ""n"", ""E"" or ""e"".")
            };

            static string ToHex(ulong value, char? separator, LetterCase letterCase)
            {
                Span<byte> bytes = stackalloc byte[sizeof(ulong)];
                var nChars = separator is null ? bytes.Length * 2 : bytes.Length * 3 - 1;
                Span<char> chars = stackalloc char[nChars];
                BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
                Hexadecimal.Write(bytes, chars, separator, letterCase);
                return new string(chars);
            }
        }
    }
}
