// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using LoRaWan;
    using Xunit;

    public abstract class KeyTests<T> where T : struct, IEquatable<T>
    {
        T Subject => Parse("0123456789abcdeffedcba9876543210");
        T Other => Parse("fedcba98765432100123456789abcdef");

        protected abstract int Size { get; }
        protected abstract T Parse(string input);
        protected abstract bool TryParse(string input, out T result);
        protected abstract bool OpEquals(T a, T b);
        protected abstract bool OpNotEquals(T a, T b);

        [Fact]
        public void Size_Returns_Width_In_Bytes()
        {
            Assert.Equal(16, Size);
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
            Assert.True(OpEquals(this.Subject, other));
        }

        [Fact]
        public void Op_Equality_Returns_False_When_Values_Differ()
        {
            Assert.False(OpEquals(this.Subject, this.Other));
        }

        [Fact]
        public void Op_Inequality_Returns_False_When_Values_Equal()
        {
            var other = this.Subject; // assignment = value copy semantics
            Assert.False(OpNotEquals(this.Subject, other));
        }

        [Fact]
        public void Op_Inequality_Returns_True_When_Values_Differ()
        {
            Assert.True(OpNotEquals(this.Subject, this.Other));
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("0123456789ABCDEFFEDCBA9876543210", this.Subject.ToString());
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
            var result = Parse(this.Subject.ToString().ToLowerInvariant());
            Assert.Equal(Subject, result);
        }

        [Fact]
        public void Parse_Throws_When_Input_Is_Invalid()
        {
            _ = Assert.Throws<FormatException>(() => AppKey.Parse("foobar"));
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
            var succeeded = TryParse(this.Subject.ToString().ToLowerInvariant(), out var result);
            Assert.True(succeeded);
            Assert.Equal(Subject, result);
        }

        [Fact]
        public void TryParse_Returns_False_When_Input_Is_Invalid()
        {
            var succeeded = TryParse("foobar", out var result);
            Assert.False(succeeded);
            Assert.True(OpEquals(result, default));
        }
    }

    public class AppKeyTests : KeyTests<AppKey>
    {
        protected override int Size => AppKey.Size;
        protected override AppKey Parse(string input) => AppKey.Parse(input);
        protected override bool TryParse(string input, out AppKey result) => AppKey.TryParse(input, out result);
        protected override bool OpEquals(AppKey a, AppKey b) => a == b;
        protected override bool OpNotEquals(AppKey a, AppKey b) => a != b;
    }

    public class AppSessionKeyTests : KeyTests<AppSessionKey>
    {
        protected override int Size => AppSessionKey.Size;
        protected override AppSessionKey Parse(string input) => AppSessionKey.Parse(input);
        protected override bool TryParse(string input, out AppSessionKey result) => AppSessionKey.TryParse(input, out result);
        protected override bool OpEquals(AppSessionKey a, AppSessionKey b) => a == b;
        protected override bool OpNotEquals(AppSessionKey a, AppSessionKey b) => a != b;
    }

    public class NetworkSessionKeyTests : KeyTests<NetworkSessionKey>
    {
        protected override int Size => NetworkSessionKey.Size;
        protected override NetworkSessionKey Parse(string input) => NetworkSessionKey.Parse(input);
        protected override bool TryParse(string input, out NetworkSessionKey result) => NetworkSessionKey.TryParse(input, out result);
        protected override bool OpEquals(NetworkSessionKey a, NetworkSessionKey b) => a == b;
        protected override bool OpNotEquals(NetworkSessionKey a, NetworkSessionKey b) => a != b;
    }
}
