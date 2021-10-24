// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using LoRaWan.NetworkServer;
    using Xunit;
    using JToken = Newtonsoft.Json.Linq.JToken;
    using Formatting = Newtonsoft.Json.Formatting;

    public class JsonReaderTest
    {
        private static string Strictify(string json) =>
            JToken.Parse(json).ToString(Formatting.None);

        private static void TestMovesReaderPastReadValue<T>(IJsonReader<T> reader, string json)
        {
            var sentinel = $"END-{Guid.NewGuid()}";
            var rdr = new Utf8JsonReader(Encoding.UTF8.GetBytes(Strictify($"[{json}, '{sentinel}']")));
            Assert.True(rdr.Read()); // start
            Assert.True(rdr.Read()); // "["
            reader.Read(ref rdr);
            Assert.Equal(JsonTokenType.String, rdr.TokenType);
            Assert.Equal(sentinel, rdr.GetString());
        }

        [Fact]
        public void String_Moves_Reader()
        {
            TestMovesReaderPastReadValue(JsonReader.String(), "'foobar'");
        }

        [Theory]
        [InlineData("", "''")]
        [InlineData("foobar", "'foobar'")]
        [InlineData("foo bar", "'foo bar'")]
        public void String_With_Valid_Input(string expected, string json)
        {
            var result = JsonReader.String().Read(Strictify(json));
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("42")]
        [InlineData("[]")]
        [InlineData("{}")]
        public void String_With_Invalid_Input(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.String().Read(Strictify(json)));
        }

        [Fact]
        public void UInt64_Moves_Reader()
        {
            TestMovesReaderPastReadValue(JsonReader.UInt64(), "42");
        }

        [Theory]
        [InlineData(42, "42")]
        public void UInt64_With_Valid_Input(ulong expected, string json)
        {
            var result = JsonReader.UInt64().Read(Strictify(json));
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("-42")]
        [InlineData("-4.2")]
        [InlineData("'foobar'")]
        [InlineData("[]")]
        [InlineData("{}")]
        public void UInt64_With_Invalid_Input(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.UInt64().Read(Strictify(json)));
        }

        [Fact]
        public void Double_Moves_Reader()
        {
            TestMovesReaderPastReadValue(JsonReader.Double(), "42");
        }

        [Theory]
        [InlineData(42, "42")]
        [InlineData(-42, "-42")]
        [InlineData(-4.2, "-4.2")]
        [InlineData(400, "4e2")]
        public void Double_With_Valid_Input(double expected, string json)
        {
            var result = JsonReader.Double().Read(Strictify(json));
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("'foobar'")]
        [InlineData("[]")]
        [InlineData("{}")]
        public void Double_With_Invalid_Input(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.Double().Read(Strictify(json)));
        }

        [Fact]
        public void Array_Moves_Reader()
        {
            TestMovesReaderPastReadValue(JsonReader.Array(JsonReader.UInt64()), "[42]");
        }

        [Theory]
        [InlineData(new ulong[] { 42 }, "[42]")]
        public void Array_With_Valid_Input(ulong[] expected, string json)
        {
            var result = JsonReader.Array(JsonReader.UInt64()).Read(Strictify(json));
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("'foobar'")]
        [InlineData("{}")]
        [InlineData("[42, null, 42]")]
        [InlineData("[42, false, 42]")]
        [InlineData("[42, true, 42]")]
        [InlineData("[42, 'foobar', 42]")]
        [InlineData("[42, [], 42]")]
        [InlineData("[42, {}, 42]")]
        public void Array_With_Invalid_Input(string json)
        {
            Assert.Throws<JsonException>(() => _ = JsonReader.Array(JsonReader.UInt64()).Read(Strictify(json)));
        }

        [Fact]
        public void Select_Invokes_Projection_Function_For_Read_Value()
        {
            var reader =
                from words in JsonReader.Array(JsonReader.String())
                select string.Join("-", from w in words select w.ToUpperInvariant());

            var result = reader.Read(Strictify("['foo', 'bar', 'baz']"));

            Assert.Equal("FOO-BAR-BAZ", result);
        }

        [Fact]
        public void Property_With_No_Default_Initializes_Property_As_Expected()
        {
            const string name = "foobar";
            var reader = JsonReader.String();

            var property = JsonReader.Property(name, reader);

            Assert.Equal(name, property.Name);
            Assert.Same(reader, property.Reader);
            Assert.Equal((false, null), property.Default);
        }

        [Fact]
        public void Property_With_Default_Initializes_Property_As_Expected()
        {
            const string name = "foobar";
            var reader = JsonReader.String();
            var @default = (true, "baz");

            var property = JsonReader.Property(name, reader, @default);

            Assert.Equal(name, property.Name);
            Assert.Same(reader, property.Reader);
            Assert.Equal(@default, property.Default);
        }

        private static readonly IJsonReader<(ulong, string)> Object2Reader =
            JsonReader.Object(JsonReader.Property("num", JsonReader.UInt64(), (true, 0UL)),
                              JsonReader.Property("str", JsonReader.String()),
                              ValueTuple.Create);

        [Theory]
        [InlineData(0 , "foobar", "{ str: 'foobar' }")]
        [InlineData(42, "foobar", "{ num: 42, str: 'foobar' }")]
        [InlineData(42, "foobar", "{ str: 'foobar', num: 42 }")]
        [InlineData(42, "foobar", "{ str: 'FOOBAR', num: -42, str: 'foobar', num: 42 }")]
        [InlineData(42, "foobar", "{ nums: [1, 2, 3], str: 'foobar', num: 42, obj: {} }")]
        public void Object2_With_Valid_Input(ulong expectedNum, string expectedStr, string json)
        {
            var (num, str) = Object2Reader.Read(Strictify(json));

            Assert.Equal(expectedNum, num);
            Assert.Equal(expectedStr, str);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("'foobar'")]
        [InlineData("[]")]
        [InlineData("{}")]
        [InlineData("{ num: '42', str: 'foobar' }")]
        [InlineData("{ NUM: 42, STR: 'foobar' }")]
        [InlineData("{ num: 42 }")]
        public void Object2_With_Invalid_Input(string json)
        {
            Assert.Throws<JsonException>(() => _ = Object2Reader.Read(Strictify(json)));
        }

        private static readonly IJsonReader<object> EitherReader =
            JsonReader.Either(JsonReader.String().AsObject(),
                              JsonReader.Array(JsonReader.UInt64().AsObject()));

        [Theory]
        [InlineData("'foobar'")]
        [InlineData("[123, 456, 789]")]
        public void Either_Moves_Reader(string json)
        {
            TestMovesReaderPastReadValue(EitherReader, json);
        }

        [Theory]
        [InlineData("foobar", "'foobar'")]
        [InlineData(new ulong[] { 123, 456, 789 }, "[123, 456, 789]")]
        [InlineData(new ulong[0], "[]")]
        public void Either_With_Valid_Input(object expected, string json)
        {
            var result = EitherReader.Read(Strictify(json));
            Assert.Equal(expected, result);
        }
        [Theory]
        [InlineData("null")]
        [InlineData("false")]
        [InlineData("true")]
        [InlineData("[12.3, 45.6, 78.9]")]
        [InlineData("{}")]
        public void Either_With_Invalid_Input(string json)
        {
            Assert.Throws<JsonException>(() => _ = EitherReader.Read(Strictify(json)));
        }
    }
}
