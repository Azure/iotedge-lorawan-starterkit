// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using LoRaWan;
    using Xunit;
    using static LoRaWan.Tests.Unit.LoRaWanTests.EuiTests;

    internal static class EuiTests
    {
        public static readonly char?[] SupportedFormats = { null, 'G', 'g', 'D', 'd', 'I', 'i', 'N', 'n', 'E', 'e' };

        public static object[][] SupportedFormatsTheoryData() =>
            SupportedFormats.Select(f => new object[] { f }).ToArray();
    }

    public abstract class EuiTests<T> where T : struct, IEquatable<T>, IFormattable
    {
        private T Subject => Parse("01-23-45-67-89-AB-CD-EF");

        private T Other => Parse("FE-DC-BA-98-76-54-32-10");

        protected abstract int Size { get; }
        protected abstract T Parse(string input);
        protected abstract bool TryParse(string input, out T result);

        private static readonly Func<T, T, bool> Equal = Operators<T>.Equality;
        private static readonly Func<T, T, bool> NotEqual = Operators<T>.Inequality;

        [Fact]
        public void Size_Returns_Width_In_Bytes()
        {
            Assert.Equal(8, Size);
        }

        [Fact]
        public void Equals_Returns_True_When_Value_Equals()
        {
            var other = this.Subject; // assignment = value copy semantics
            Assert.True(this.Subject.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Value_Is_Different()
        {
            var other = this.Other;
            Assert.False(this.Subject.Equals(other));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Different()
        {
            Assert.False(this.Subject.Equals(new object()));
        }

        [Fact]
        public void Equals_Returns_False_When_Other_Type_Is_Null()
        {
            Assert.False(this.Subject.Equals(null));
        }

        [Fact]
        public void Op_Equality_Returns_True_When_Values_Equal()
        {
            var other = this.Subject; // assignment = value copy semantics
            Assert.True(Equal(this.Subject, other));
        }

        [Fact]
        public void Op_Equality_Returns_False_When_Values_Differ()
        {
            Assert.False(Equal(this.Subject, this.Other));
        }

        [Fact]
        public void Op_Inequality_Returns_False_When_Values_Equal()
        {
            var other = this.Subject; // assignment = value copy semantics
            Assert.False(NotEqual(this.Subject, other));
        }

        [Fact]
        public void Op_Inequality_Returns_True_When_Values_Differ()
        {
            Assert.True(NotEqual(this.Subject, this.Other));
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("01-23-45-67-89-AB-CD-EF", this.Subject.ToString());
        }

        [Theory]
        [InlineData(null, "01-23-45-67-89-AB-CD-EF")]
        [InlineData("G", "01-23-45-67-89-AB-CD-EF")]
        [InlineData("D", "01-23-45-67-89-AB-CD-EF")]
        [InlineData("g", "01-23-45-67-89-ab-cd-ef")]
        [InlineData("d", "01-23-45-67-89-ab-cd-ef")]
        [InlineData("E", "01:23:45:67:89:AB:CD:EF")]
        [InlineData("e", "01:23:45:67:89:ab:cd:ef")]
        [InlineData("I", "0123:4567:89AB:CDEF")]
        [InlineData("i", "0123:4567:89ab:cdef")]
        [InlineData("N", "0123456789ABCDEF")]
        [InlineData("n", "0123456789abcdef")]
        public void ToString_Returns_Correctly_Formatted_String(string format, string expectedRepresentation)
        {
            Assert.Equal(expectedRepresentation, Subject.ToString(format, null));
        }

        [Fact]
        public void ToString_Throws_FormatException_When_Format_Is_Not_Supported()
        {
            var ex = Assert.Throws<FormatException>(() => Subject.ToString("z", null));

            foreach (var c in SupportedFormats.Where(c => c != null).Cast<char>())
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
        [InlineData("{0}", "01-23-45-67-89-AB-CD-EF")]
        [InlineData("{0:G}", "01-23-45-67-89-AB-CD-EF")]
        [InlineData("{0:D}", "01-23-45-67-89-AB-CD-EF")]
        [InlineData("{0:g}", "01-23-45-67-89-ab-cd-ef")]
        [InlineData("{0:d}", "01-23-45-67-89-ab-cd-ef")]
        [InlineData("{0:E}", "01:23:45:67:89:AB:CD:EF")]
        [InlineData("{0:e}", "01:23:45:67:89:ab:cd:ef")]
        [InlineData("{0:I}", "0123:4567:89AB:CDEF")]
        [InlineData("{0:i}", "0123:4567:89ab:cdef")]
        [InlineData("{0:N}", "0123456789ABCDEF")]
        [InlineData("{0:n}", "0123456789abcdef")]
        public void String_Interpolation_Success_Case(string format, string expected)
        {
            var result = string.Format(CultureInfo.InvariantCulture, format, Subject);
            Assert.Equal(expected, result);
        }
    }

    public class DevEuiTests : EuiTests<DevEui>
    {
        protected override int Size => DevEui.Size;
        protected override DevEui Parse(string input) => DevEui.Parse(input);
        protected override bool TryParse(string input, out DevEui result) => DevEui.TryParse(input, out result);
    }

    public class JoinEuiTests : EuiTests<JoinEui>
    {
        protected override int Size => JoinEui.Size;
        protected override JoinEui Parse(string input) => JoinEui.Parse(input);
        protected override bool TryParse(string input, out JoinEui result) => JoinEui.TryParse(input, out result);

    }

    public class StationEuiTests : EuiTests<StationEui>
    {
        protected override int Size => StationEui.Size;
        protected override StationEui Parse(string input) => StationEui.Parse(input);
        protected override bool TryParse(string input, out StationEui result) => StationEui.TryParse(input, out result);
    }
}
