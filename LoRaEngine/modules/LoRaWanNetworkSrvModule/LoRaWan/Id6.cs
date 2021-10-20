// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Buffers.Binary;

    // ID6
    //
    // An alternative syntax for representing an EUI, which mimics the encoding rules of IPv6
    // addresses. While IPv6 encoding operates on 128 bits grouped into eight (8) 16-bit blocks,
    // ID6 operates on four (4) groups of 16-bit blocks. Below are some examples of ID6 and EUI
    // pairs:
    //
    // - ::0 = 00-00-00-00-00-00-00-00
    // - 1:: = 00-01-00-00-00-00-00-00
    // - ::a:b = 00-00-00-00-00-0a-00-0b
    // - f::1 = 00-0f-00-00-00-00-00-01
    // - f:a123:f8:100 = 00-0f-a1-23-00-f8-01-00
    //
    // Source: https://doc.sm.tc/station/glossary.html#term-id6
    // Copyright 2019, Semtech Corp

    public static class Id6
    {
        [Flags]
        public enum FormatOptions
        {
            None = 0,
            Lowercase = 1,
            FixedWidth = 2,
        }

        private enum FormatterState
        {
            Init,
            Word,
            WordColonBlank,
            Blank,
            BlankColonBlank,
            ColonColon,
            ColonColonWord,
        }

        public static string Format(ulong value, FormatOptions options = FormatOptions.None) =>
            new(Format(value, stackalloc char[(sizeof(ulong) * 2) + 3 /* colons */], options));

        public static Span<char> Format(ulong value, Span<char> output, FormatOptions options = FormatOptions.None)
        {
            if (output.Length < 2) throw new ArgumentException(null, nameof(output));

            var fixedWidth = (options & FormatOptions.FixedWidth) != 0;

            if (!fixedWidth && value == 0) // bail out early with the 0 == "::" case
            {
                output[0] = output[1] = ':';
                return output[..2];
            }

            var casing = (options & FormatOptions.Lowercase) != 0 ? LetterCase.Lower : LetterCase.Upper;

            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            Span<char> chars = stackalloc char[(bytes.Length * 2) + 3 /* colons */];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);

            Span<char> word = stackalloc char[sizeof(ushort) * 2];
            var state = FormatterState.Init;

            var i = 0;
            for (; !bytes.IsEmpty; bytes = bytes[sizeof(ushort)..])
            {
                bool colon;
#pragma warning disable IDE0072 // Add missing cases (false positive)
                (colon, state) = state switch
#pragma warning restore IDE0072 // Add missing cases
                {
                    var s and (FormatterState.Word or FormatterState.Blank or FormatterState.ColonColonWord)  => (true, s),
                    FormatterState.WordColonBlank or FormatterState.BlankColonBlank => (true, FormatterState.ColonColon),
                    var s => (false, s)
                };
                if (colon)
                    chars[i++] = ':';
                var @short = BinaryPrimitives.ReadUInt16BigEndian(bytes);
#pragma warning disable IDE0058 // Expression value is never used
                Hexadecimal.Write(@short, word, casing);
#pragma warning restore IDE0058 // Expression value is never used
                var tw = fixedWidth ? word : word.TrimStart('0');
                if (tw.Length != 0)
                {
                    tw.CopyTo(chars[i..]);
                    i += tw.Length;
#pragma warning disable IDE0072 // Add missing cases (false positive)
                    state = (state, colon) switch
#pragma warning restore IDE0072 // Add missing cases
                    {
                        (FormatterState.Init, _) or (FormatterState.Word or FormatterState.Blank, true) => FormatterState.Word,
                        (FormatterState.ColonColon, _) or (FormatterState.ColonColonWord, true) => FormatterState.ColonColonWord,
                        var (s, _) => s,
                    };
                }
                else
                {
#pragma warning disable IDE0072 // Add missing cases (false positive)
                    state = (state, colon) switch
#pragma warning restore IDE0072 // Add missing cases
                    {
                        (FormatterState.Init, _) => FormatterState.Blank,
                        (FormatterState.Word, true) => FormatterState.WordColonBlank,
                        (FormatterState.Blank, true) => FormatterState.BlankColonBlank,
                        var (s, _) => s,
                    };
                }
            }

            if (output.Length < i)
                throw new ArgumentException(null, nameof(output));

            chars[..i].CopyTo(output);
            return output[..i];
        }

        private struct WordAccumulator
        {
            private byte digits;
            private ushort value;

            public static implicit operator ushort(WordAccumulator acc) => acc.value;

            public void Reset() => (this.digits, this.value) = (0, 0);

            public bool ParseHexDigit(char ch)
            {
                if (this.digits == 4)
                    return false;
                int d;
                switch (ch)
                {
                    case >= '0' and <= '9': d = ch - '0'; break;
                    case >= 'a' and <= 'f': d = ch - 'a' + 10; break;
                    case >= 'A' and <= 'F': d = ch - 'A' + 10; break;
                    default: return false;
                }
                this.value = checked((ushort)((this.value << 4) + d));
                this.digits++;
                return true;
            }
        }

        private enum ParserState
        {
            BeforeColonColon,
            ColonBeforeColonColon,
            ColonColon,
            AfterColonColon,
            ColonAfterColonColon,
        }

        public static bool TryParse(ReadOnlySpan<char> chars, out ulong result)
        {
            result = 0;

            if (chars.IsEmpty)
                return false;

            var state = ParserState.BeforeColonColon;
            Span<ushort> shorts = stackalloc ushort[4];

            var si = 0;
            var cci = -1;
#pragma warning disable SA1129 // Do not use default value type constructor
            var wa = new WordAccumulator();
#pragma warning restore SA1129 // Do not use default value type constructor

            for (; !chars.IsEmpty; chars = chars[1..])
            {
                var ch = chars[0];

                restate:
                switch (state)
                {
                    case ParserState.BeforeColonColon:
                    case ParserState.AfterColonColon:
                    {
                        if (ch == ':')
                        {
                            shorts[si++] = wa;
                            if (si == 4)
                                return false;
                            wa.Reset();
                            state = state == ParserState.BeforeColonColon ? ParserState.ColonBeforeColonColon : ParserState.ColonAfterColonColon;
                        }
                        else if (!wa.ParseHexDigit(ch))
                        {
                            return false;
                        }

                        break;
                    }
                    case ParserState.ColonBeforeColonColon:
                    {
                        if (ch == ':')
                        {
                            cci = si;
                            state = ParserState.ColonColon;
                        }
                        else
                        {
                            state = ParserState.BeforeColonColon;
                            goto restate;
                        }

                        break;
                    }
                    case ParserState.ColonAfterColonColon:
                    {
                        if (ch == ':')
                            return false;
                        state = ParserState.AfterColonColon;
                        goto restate;
                    }
                    case ParserState.ColonColon:
                    {
                        if (!wa.ParseHexDigit(ch))
                            return false;
                        state = ParserState.AfterColonColon;
                        break;
                    }
                    default:
                        return false;
                }
            }

            shorts[si] = wa;

            if (state is ParserState.ColonBeforeColonColon or ParserState.BeforeColonColon && si < 3)
                return false;

            ref var a = ref shorts[0];
            ref var b = ref shorts[1];
            ref var c = ref shorts[2];
            ref var d = ref shorts[3];

            switch (cci, si)
            {
                case (1, 1): (d, b) = (b, 0); break;
                case (1, 2): (d, c, b) = (c, b, 0); break;
                case (2, 2): (d, c) = (c, 0); break;
                default: break;
            }

            result = ((ulong)a << 48) | ((ulong)b << 32) | ((ulong)c << 16) | d;

            return true;
        }
    }
}
