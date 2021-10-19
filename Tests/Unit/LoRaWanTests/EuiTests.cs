// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using LoRaWan;
    using Xunit;

    public abstract class EuiTests<T> where T : struct, IEquatable<T>
    {
        T Subject => Parse("01-23-45-67-89-AB-CD-EF");
        T Other => Parse("FE-DC-BA-98-76-54-32-10");

        protected abstract int Size { get; }
        protected abstract T Parse(string input);
        protected abstract bool TryParse(string input, out T result);

        static readonly Func<T, T, bool> Equal = Operators<T>.Equality;
        static readonly Func<T, T, bool> NotEqual = Operators<T>.Inequality;

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

        [Fact]
        public void Parse_Returns_Parsed_Value_When_Input_Is_Valid()
        {
            var result = Parse(this.Subject.ToString());
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
            _ = Assert.Throws<FormatException>(() => DevEui.Parse("foobar"));
        }

        [Fact]
        public void TryParse_Returns_True_And_Outputs_Parsed_Value_When_Input_Is_Valid()
        {
            var succeeded = TryParse(this.Subject.ToString(), out var result);
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
            var succeeded = DevEui.TryParse("foobar", out var result);
            Assert.False(succeeded);
            Assert.True(result == default);
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
