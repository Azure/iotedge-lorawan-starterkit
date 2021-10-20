// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using LoRaWan;
    using Xunit;
    using static LoRaWan.Id6.FormatOptions;

    public class Id6Tests
    {
        [Theory]

        [InlineData(None, 0UL, "::")]
        [InlineData(None, 0x000f_a123_00f8_0100UL, "F:A123:F8:100")]
        [InlineData(None, 0xdead_0000_0000_0000UL, "DEAD::")]
        [InlineData(None, 0x0000_dead_0000_0000UL, ":DEAD::")]
        [InlineData(None, 0x0000_0000_dead_0000UL, "::DEAD:")]
        [InlineData(None, 0x0000_0000_0000_deadUL, "::DEAD")]
        [InlineData(None, 0xdead_0000_0000_c0deUL, "DEAD::C0DE")]
        [InlineData(None, 0xdead_c0de_0000_0000UL, "DEAD:C0DE::")]
        [InlineData(None, 0x0000_dead_c0de_0000UL, ":DEAD:C0DE:")]
        [InlineData(None, 0x0000_0000_dead_c0deUL, "::DEAD:C0DE")]
        [InlineData(None, 0xdead_0000_c0de_0000UL, "DEAD::C0DE:")]
        [InlineData(None, 0x0000_dead_0000_c0deUL, ":DEAD::C0DE")]
        [InlineData(None, 0xfeed_dead_cafe_c0deUL, "FEED:DEAD:CAFE:C0DE")]

        [InlineData(Lowercase, 0UL, "::")]
        [InlineData(Lowercase, 0x000f_a123_00f8_0100UL, "f:a123:f8:100")]
        [InlineData(Lowercase, 0xdead_0000_0000_0000UL, "dead::")]
        [InlineData(Lowercase, 0x0000_dead_0000_0000UL, ":dead::")]
        [InlineData(Lowercase, 0x0000_0000_dead_0000UL, "::dead:")]
        [InlineData(Lowercase, 0x0000_0000_0000_deadUL, "::dead")]
        [InlineData(Lowercase, 0xdead_0000_0000_c0deUL, "dead::c0de")]
        [InlineData(Lowercase, 0xdead_c0de_0000_0000UL, "dead:c0de::")]
        [InlineData(Lowercase, 0x0000_dead_c0de_0000UL, ":dead:c0de:")]
        [InlineData(Lowercase, 0x0000_0000_dead_c0deUL, "::dead:c0de")]
        [InlineData(Lowercase, 0xdead_0000_c0de_0000UL, "dead::c0de:")]
        [InlineData(Lowercase, 0x0000_dead_0000_c0deUL, ":dead::c0de")]
        [InlineData(Lowercase, 0xfeed_dead_cafe_c0deUL, "feed:dead:cafe:c0de")]

        [InlineData(FixedWidth, 0x0000_0000_0000_0000UL, "0000:0000:0000:0000")]
        [InlineData(FixedWidth, 0x000f_a123_00f8_0100UL, "000F:A123:00F8:0100")]
        [InlineData(FixedWidth, 0xdead_0000_0000_0000UL, "DEAD:0000:0000:0000")]
        [InlineData(FixedWidth, 0x0000_dead_0000_0000UL, "0000:DEAD:0000:0000")]
        [InlineData(FixedWidth, 0x0000_0000_dead_0000UL, "0000:0000:DEAD:0000")]
        [InlineData(FixedWidth, 0x0000_0000_0000_deadUL, "0000:0000:0000:DEAD")]
        [InlineData(FixedWidth, 0xdead_0000_0000_c0deUL, "DEAD:0000:0000:C0DE")]
        [InlineData(FixedWidth, 0xdead_c0de_0000_0000UL, "DEAD:C0DE:0000:0000")]
        [InlineData(FixedWidth, 0x0000_dead_c0de_0000UL, "0000:DEAD:C0DE:0000")]
        [InlineData(FixedWidth, 0x0000_0000_dead_c0deUL, "0000:0000:DEAD:C0DE")]
        [InlineData(FixedWidth, 0xdead_0000_c0de_0000UL, "DEAD:0000:C0DE:0000")]
        [InlineData(FixedWidth, 0x0000_dead_0000_c0deUL, "0000:DEAD:0000:C0DE")]
        [InlineData(FixedWidth, 0xfeed_dead_cafe_c0deUL, "FEED:DEAD:CAFE:C0DE")]

        [InlineData(FixedWidth | Lowercase, 0x0000_0000_0000_0000UL, "0000:0000:0000:0000")]
        [InlineData(FixedWidth | Lowercase, 0x000f_a123_00f8_0100UL, "000f:a123:00f8:0100")]
        [InlineData(FixedWidth | Lowercase, 0xdead_0000_0000_0000UL, "dead:0000:0000:0000")]
        [InlineData(FixedWidth | Lowercase, 0x0000_dead_0000_0000UL, "0000:dead:0000:0000")]
        [InlineData(FixedWidth | Lowercase, 0x0000_0000_dead_0000UL, "0000:0000:dead:0000")]
        [InlineData(FixedWidth | Lowercase, 0x0000_0000_0000_deadUL, "0000:0000:0000:dead")]
        [InlineData(FixedWidth | Lowercase, 0xdead_0000_0000_c0deUL, "dead:0000:0000:c0de")]
        [InlineData(FixedWidth | Lowercase, 0xdead_c0de_0000_0000UL, "dead:c0de:0000:0000")]
        [InlineData(FixedWidth | Lowercase, 0x0000_dead_c0de_0000UL, "0000:dead:c0de:0000")]
        [InlineData(FixedWidth | Lowercase, 0x0000_0000_dead_c0deUL, "0000:0000:dead:c0de")]
        [InlineData(FixedWidth | Lowercase, 0xdead_0000_c0de_0000UL, "dead:0000:c0de:0000")]
        [InlineData(FixedWidth | Lowercase, 0x0000_dead_0000_c0deUL, "0000:dead:0000:c0de")]
        [InlineData(FixedWidth | Lowercase, 0xfeed_dead_cafe_c0deUL, "feed:dead:cafe:c0de")]

        public void Format(Id6.FormatOptions options, ulong input, string expected)
        {
            Assert.Equal(expected, Id6.Format(input, options));
        }

        [Theory]
        [InlineData("::"         , 0UL)]
        [InlineData("0:0:0:0"    , 0UL)]
        [InlineData("0:0:0:1a"   , 0x0000_0000_0000_001aUL)]
        [InlineData("0:0:0:2b"   , 0x0000_0000_0000_002bUL)]
        [InlineData("0:0:0:3c"   , 0x0000_0000_0000_003cUL)]
        [InlineData("0:0:1a:0"   , 0x0000_0000_001a_0000UL)]
        [InlineData("0:0:2b:0"   , 0x0000_0000_002b_0000UL)]
        [InlineData("0:0:3c:0"   , 0x0000_0000_003c_0000UL)]
        [InlineData("0:1a:0:0"   , 0x0000_001a_0000_0000UL)]
        [InlineData("0:2b:0:0"   , 0x0000_002b_0000_0000UL)]
        [InlineData("0:3c:0:0"   , 0x0000_003c_0000_0000UL)]
        [InlineData("1a:0:0:0"   , 0x001a_0000_0000_0000UL)]
        [InlineData("2b:0:0:0"   , 0x002b_0000_0000_0000UL)]
        [InlineData("3c:0:0:0"   , 0x003c_0000_0000_0000UL)]
        [InlineData("1a:2b:3c:4d", 0x001a_002b_003c_004dUL)]
        [InlineData(":2b:3c:4d"  , 0x0000_002b_003c_004dUL)]
        [InlineData("0:2b:3c:4d" , 0x0000_002b_003c_004dUL)]
        [InlineData("1a::3c:4d"  , 0x001a_0000_003c_004dUL)]
        [InlineData("1a:0:3c:4d" , 0x001a_0000_003c_004dUL)]
        [InlineData("1a:2b::4d"  , 0x001a_002b_0000_004dUL)]
        [InlineData("1a:2b:0:4d" , 0x001a_002b_0000_004dUL)]
        [InlineData("1a:2b:3c:"  , 0x001a_002b_003c_0000UL)]
        [InlineData("1a:2b:3c:0" , 0x001a_002b_003c_0000UL)]
        [InlineData("1a::"       , 0x001a_0000_0000_0000UL)]
        [InlineData("1a::0"      , 0x001a_0000_0000_0000UL)]
        [InlineData(":2b::"      , 0x0000_002b_0000_0000UL)]
        [InlineData("0:2b::"     , 0x0000_002b_0000_0000UL)]
        [InlineData("0:2b::0"    , 0x0000_002b_0000_0000UL)]
        [InlineData("::3c:"      , 0x0000_0000_003c_0000UL)]
        [InlineData("0::3c:"     , 0x0000_0000_003c_0000UL)]
        [InlineData("0::3c:0"    , 0x0000_0000_003c_0000UL)]
        [InlineData("::4d"       , 0x0000_0000_0000_004dUL)]
        [InlineData("0::4d"      , 0x0000_0000_0000_004dUL)]
        [InlineData("1a::4d"     , 0x001a_0000_0000_004dUL)]
        [InlineData(":2b::4d"    , 0x0000_002b_0000_004dUL)]
        [InlineData("0:2b::4d"   , 0x0000_002b_0000_004dUL)]
        [InlineData(":2b:3c:"    , 0x0000_002b_003c_0000UL)]
        [InlineData("0:2b:3c:0"  , 0x0000_002b_003c_0000UL)]
        [InlineData("1a:2b::"    , 0x001a_002b_0000_0000UL)]
        [InlineData("1a:2b::0"   , 0x001a_002b_0000_0000UL)]
        [InlineData("::3c:4d"    , 0x0000_0000_003c_004dUL)]
        [InlineData("0::3c:4d"   , 0x0000_0000_003c_004dUL)]
        // Following test data is taken from examples at:
        // https://doc.sm.tc/station/glossary.html#term-id6
        [InlineData("::0"        , 0x0000_0000_0000_0000UL)]
        [InlineData("1::"        , 0x0001_0000_0000_0000UL)]
        [InlineData("::a:b"      , 0x0000_0000_000a_000bUL)]
        [InlineData("f::1"       , 0x000f_0000_0000_0001UL)]
        [InlineData("f:a123:f8:100", 0x000f_a123_00f8_0100UL)]
        public void TryParse_Valid(string input, ulong expected)
        {
            var succeeded = Id6.TryParse(input, out var result);
            Assert.True(succeeded);
            Assert.True(expected == result, $"{result:x16} does not equal the expected value of {expected:x16}.");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(":")]
        [InlineData(":::")]
        [InlineData("::1::")]
        [InlineData("1")]
        [InlineData("1:")]
        [InlineData("1:2")]
        [InlineData("1:2:")]
        [InlineData("1:2:3")]
        [InlineData("1:2:3:4:")]
        [InlineData("1:2:3:4::")]
        [InlineData(":1:2:3:4")]
        [InlineData("::1:2:3:4")]
        [InlineData("1:2:3:4:5")]
        [InlineData("12345::")]
        [InlineData(":12345::")]
        [InlineData("::12345::")]
        [InlineData("g:1:1:1")]
        [InlineData("1:g:1:1")]
        [InlineData("1:1:g:1")]
        [InlineData("1:1:1:g")]
        public void TryParse_Invalid(string input)
        {
            var succeeded = Id6.TryParse(input, out var result);
            Assert.False(succeeded);
            Assert.Equal(0UL, result);
        }
    }
}
