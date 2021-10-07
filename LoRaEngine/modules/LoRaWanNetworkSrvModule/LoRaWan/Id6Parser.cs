// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;


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

    public static class Id6Parser
    {
        struct WordAccumulator
        {
            byte digits;
            ushort value;

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

        enum ParserState
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
            }

            result = (ulong)a << 48 | (ulong)b << 32 | (ulong)c << 16 | d;

            return true;
        }
    }
}
