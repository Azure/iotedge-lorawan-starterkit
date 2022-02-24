// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using System.Globalization;
    using System.Linq;
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;

    internal static class EuiTests
    {
        public static readonly char?[] SupportedFormats = { null, 'G', 'g', 'D', 'd', 'I', 'i', 'N', 'n', 'E', 'e' };
    }

    public abstract class EuiTests<T> where T : struct, IEquatable<T>, IFormattable
    {
        protected T Subject => Parse("01-23-45-67-89-AB-CD-EF");

        protected abstract int Size { get; }
        protected abstract T Parse(string input);
        protected abstract bool TryParse(string input, out T result);
        protected abstract string ToHex(T eui);
        protected abstract string ToHex(T eui, char? separator);
        protected abstract string ToHex(T eui, LetterCase letterCase);
        protected abstract string ToHex(T eui, char? separator, LetterCase letterCase);

        [Fact]
        public void Size_Returns_Width_In_Bytes()
        {
            Assert.Equal(8, Size);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("0123456789ABCDEF", Subject.ToString());
        }

        [Theory]
        [InlineData(null, "0123456789ABCDEF")]
        [InlineData("G", "0123456789ABCDEF")]
        [InlineData("g", "0123456789abcdef")]
        [InlineData("N", "0123456789ABCDEF")]
        [InlineData("n", "0123456789abcdef")]
        [InlineData("D", "01-23-45-67-89-AB-CD-EF")]
        [InlineData("d", "01-23-45-67-89-ab-cd-ef")]
        [InlineData("E", "01:23:45:67:89:AB:CD:EF")]
        [InlineData("e", "01:23:45:67:89:ab:cd:ef")]
        [InlineData("I", "0123:4567:89AB:CDEF")]
        [InlineData("i", "0123:4567:89ab:cdef")]
        public void ToString_Returns_Correctly_Formatted_String(string format, string expectedRepresentation)
        {
            Assert.Equal(expectedRepresentation, Subject.ToString(format, null));
        }

        [Fact]
        public void ToString_Throws_FormatException_When_Format_Is_Not_Supported()
        {
            var ex = Assert.Throws<FormatException>(() => Subject.ToString("z", null));

            foreach (var c in EuiTests.SupportedFormats.Where(c => c != null).Cast<char>())
            {
                Assert.True(ex.Message.Contains(c, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void Parse_Returns_Parsed_Value_When_Input_Is_Valid()
        {
            var result = Parse(this.Subject.ToString());
            Assert.Equal(Subject, result);
        }

#pragma warning disable CA1000 // Do not declare static members on generic types (necessary for unit tests)
        public static TheoryData<string> SupportedFormatsTheoryData() =>
#pragma warning restore CA1000 // Do not declare static members on generic types
            TheoryDataFactory.From(EuiTests.SupportedFormats.Select(c => c?.ToString()));

        [Theory]
        [MemberData(nameof(SupportedFormatsTheoryData))]
        public void Parse_Returns_Parsed_Value_With_Valid_Format(string format)
        {
            var result = Parse(Subject.ToString(format, null));
            Assert.Equal(Subject, result);
        }

        [Fact]
        public void Parse_Input_Is_Case_Insensitive()
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            var result = Parse(this.Subject.ToString().ToLowerInvariant());
#pragma warning restore CA1308 // Normalize strings to uppercase
            Assert.Equal(Subject, result);
        }

        [Fact]
        public void Parse_Throws_When_Input_Is_Invalid()
        {
            _ = Assert.Throws<FormatException>(() => Parse("foobar"));
        }

        [Fact]
        public void TryParse_Returns_True_And_Outputs_Parsed_Value_When_Input_Is_Valid()
        {
            var succeeded = TryParse(this.Subject.ToString(), out var result);
            Assert.True(succeeded);
            Assert.Equal(Subject, result);
        }

        [Theory]
        [MemberData(nameof(SupportedFormatsTheoryData))]
        public void TryParse_Returns_Parsed_Value_With_Valid_Format(string format)
        {
            var succeeded = TryParse(Subject.ToString(format, null), out var result);
            Assert.True(succeeded);
            Assert.Equal(Subject, result);
        }

        [Fact]
        public void TryParse_Input_Is_Case_Insensitive()
        {
#pragma warning disable CA1308 // Normalize strings to uppercase
            var succeeded = TryParse(this.Subject.ToString().ToLowerInvariant(), out var result);
#pragma warning restore CA1308 // Normalize strings to uppercase
            Assert.True(succeeded);
            Assert.Equal(Subject, result);
        }

        [Fact]
        public void TryParse_Returns_False_When_Input_Is_Invalid()
        {
            var succeeded = TryParse("foobar", out var result);
            Assert.False(succeeded);
            Assert.Equal(default, result);
        }

        [Theory]
        [InlineData("{0}", "0123456789ABCDEF")]
        [InlineData("{0:G}", "0123456789ABCDEF")]
        [InlineData("{0:g}", "0123456789abcdef")]
        [InlineData("{0:N}", "0123456789ABCDEF")]
        [InlineData("{0:n}", "0123456789abcdef")]
        [InlineData("{0:D}", "01-23-45-67-89-AB-CD-EF")]
        [InlineData("{0:d}", "01-23-45-67-89-ab-cd-ef")]
        [InlineData("{0:E}", "01:23:45:67:89:AB:CD:EF")]
        [InlineData("{0:e}", "01:23:45:67:89:ab:cd:ef")]
        [InlineData("{0:I}", "0123:4567:89AB:CDEF")]
        [InlineData("{0:i}", "0123:4567:89ab:cdef")]
        public void String_Interpolation_Success_Case(string format, string expected)
        {
            var result = string.Format(CultureInfo.InvariantCulture, format, Subject);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToHex_Returns_Eui_Formatted_Using_UpperCase_Digits()
        {
            Assert.Equal("0123456789ABCDEF", ToHex(Subject));
        }

        [Theory]
        [InlineData("0123456789ABCDEF", null)]
        [InlineData("01-23-45-67-89-AB-CD-EF", '-')]
        public void ToHex_With_Separator(string expected, char? separator)
        {
            Assert.Equal(expected, ToHex(Subject, separator));
        }

        [Theory]
        [InlineData("0123456789ABCDEF", LetterCase.Upper)]
        [InlineData("0123456789abcdef", LetterCase.Lower)]
        public void ToHex_With_Case(string expected, LetterCase letterCase)
        {
            Assert.Equal(expected, ToHex(Subject, letterCase));
        }

        [Theory]
        [InlineData("0123456789ABCDEF", null, LetterCase.Upper)]
        [InlineData("01-23-45-67-89-AB-CD-EF", '-', LetterCase.Upper)]
        [InlineData("0123456789abcdef", null, LetterCase.Lower)]
        [InlineData("01-23-45-67-89-ab-cd-ef", '-', LetterCase.Lower)]
        public void ToHex_With_Separator_With_Case(string expected, char? separator, LetterCase letterCase)
        {
            Assert.Equal(expected, ToHex(Subject, separator, letterCase));
        }
    }

    public abstract class DevOrStationEuiTests<T> : EuiTests<T>
        where T : struct, IEquatable<T>, IFormattable
    {
        protected abstract bool IsValid(T eui);

        [Theory]
        [InlineData(false, "::")]
        [InlineData(false, "ffff:ffff:ffff:ffff")]
        [InlineData(true, "::1")]
        [InlineData(true, "0123:4567:89ab:cdef")]
        public void Validation(bool expected, string input)
        {
            var subject = Parse(input);
            Assert.Equal(expected, IsValid(subject));
        }
    }

    public class DevEuiTests : DevOrStationEuiTests<DevEui>
    {
        protected override int Size => DevEui.Size;
        protected override DevEui Parse(string input) => DevEui.Parse(input);
        protected override bool TryParse(string input, out DevEui result) => DevEui.TryParse(input, out result);
        protected override bool IsValid(DevEui eui) => eui.IsValid;
        protected override string ToHex(DevEui eui) => eui.ToHex();
        protected override string ToHex(DevEui eui, char? separator) => eui.ToHex(separator);
        protected override string ToHex(DevEui eui, LetterCase letterCase) => eui.ToHex(letterCase);
        protected override string ToHex(DevEui eui, char? separator, LetterCase letterCase) => eui.ToHex(separator, letterCase);

        [Theory]
        [InlineData(true, 0UL, "::", EuiParseOptions.None)]
        [InlineData(false, 0UL, "::", EuiParseOptions.ForbidInvalid)]
        [InlineData(true, ulong.MaxValue, "ffff:ffff:ffff:ffff", EuiParseOptions.None)]
        [InlineData(false, 0UL, "ffff:ffff:ffff:ffff", EuiParseOptions.ForbidInvalid)]
        [InlineData(true, 1UL, "::1", EuiParseOptions.None)]
        public void TryParse_With_Options(bool succeeds, ulong eui, string input, EuiParseOptions options)
        {
            var succeeded = DevEui.TryParse(input, options, out var result);

            Assert.Equal(succeeds, succeeded);
            Assert.Equal(eui, result.AsUInt64);
        }
    }

    public class JoinEuiTests : EuiTests<JoinEui>
    {
        protected override int Size => JoinEui.Size;
        protected override JoinEui Parse(string input) => JoinEui.Parse(input);
        protected override bool TryParse(string input, out JoinEui result) => JoinEui.TryParse(input, out result);
        protected override string ToHex(JoinEui eui) => eui.ToHex();
        protected override string ToHex(JoinEui eui, char? separator) => eui.ToHex(separator);
        protected override string ToHex(JoinEui eui, LetterCase letterCase) => eui.ToHex(letterCase);
        protected override string ToHex(JoinEui eui, char? separator, LetterCase letterCase) => eui.ToHex(separator, letterCase);
    }

    public class StationEuiTests : DevOrStationEuiTests<StationEui>
    {
        protected override int Size => StationEui.Size;
        protected override StationEui Parse(string input) => StationEui.Parse(input);
        protected override bool TryParse(string input, out StationEui result) => StationEui.TryParse(input, out result);
        protected override bool IsValid(StationEui eui) => eui.IsValid;
        protected override string ToHex(StationEui eui) => eui.ToHex();
        protected override string ToHex(StationEui eui, char? separator) => eui.ToHex(separator);
        protected override string ToHex(StationEui eui, LetterCase letterCase) => eui.ToHex(letterCase);
        protected override string ToHex(StationEui eui, char? separator, LetterCase letterCase) => eui.ToHex(separator, letterCase);
    }
}
