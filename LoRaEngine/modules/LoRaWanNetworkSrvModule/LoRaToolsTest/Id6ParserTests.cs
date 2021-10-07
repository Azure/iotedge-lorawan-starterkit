// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using LoRaWan;
    using Xunit;

    public class Id6ParserTests
    {
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
        public void Success(string input, ulong expected)
        {
            var succeeded = Id6Parser.TryParse(input, out var result);
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
        public void Failure(string input)
        {
            var succeeded = Id6Parser.TryParse(input, out var result);
            Assert.False(succeeded);
            Assert.Equal(0UL, result);
        }
    }
}
