// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System;
    using LoRaWan;
    using Xunit;

    public abstract class KeyTests<T> where T : struct, IEquatable<T>
    {
        private T Subject => Parse("0123456789abcdeffedcba9876543210");

        protected abstract int Size { get; }
        protected abstract T Parse(string input);
        protected abstract bool TryParse(string input, out T result);
        protected abstract Span<byte> Write(T instance, Span<byte> buffer);
        protected abstract T Read(ReadOnlySpan<byte> buffer);

        [Fact]
        public void Size_Returns_Width_In_Bytes()
        {
            Assert.Equal(16, Size);
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
#pragma warning disable CA1308 // Normalize strings to uppercase
            var result = Parse(this.Subject.ToString().ToLowerInvariant());
#pragma warning restore CA1308 // Normalize strings to uppercase
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

        [Fact]
        public void Write_Read_Composition_Is_Identity()
        {
            var buffer = new byte[Size];
            _ = Write(Subject, buffer);
            var result = Read(buffer);
            Assert.Equal(Subject, result);
        }
    }

    public class AppKeyTests : KeyTests<AppKey>
    {
        protected override int Size => AppKey.Size;
        protected override AppKey Parse(string input) => AppKey.Parse(input);
        protected override bool TryParse(string input, out AppKey result) => AppKey.TryParse(input, out result);
        protected override AppKey Read(ReadOnlySpan<byte> buffer) => AppKey.Read(buffer);
        protected override Span<byte> Write(AppKey instance, Span<byte> buffer) => instance.Write(buffer);
    }

    public class AppSessionKeyTests : KeyTests<AppSessionKey>
    {
        protected override int Size => AppSessionKey.Size;
        protected override AppSessionKey Parse(string input) => AppSessionKey.Parse(input);
        protected override bool TryParse(string input, out AppSessionKey result) => AppSessionKey.TryParse(input, out result);
        protected override AppSessionKey Read(ReadOnlySpan<byte> buffer) => AppSessionKey.Read(buffer);
        protected override Span<byte> Write(AppSessionKey instance, Span<byte> buffer) => instance.Write(buffer);
    }

    public class NetworkSessionKeyTests : KeyTests<NetworkSessionKey>
    {
        protected override int Size => NetworkSessionKey.Size;
        protected override NetworkSessionKey Parse(string input) => NetworkSessionKey.Parse(input);
        protected override bool TryParse(string input, out NetworkSessionKey result) => NetworkSessionKey.TryParse(input, out result);
        protected override NetworkSessionKey Read(ReadOnlySpan<byte> buffer) => NetworkSessionKey.Read(buffer);
        protected override Span<byte> Write(NetworkSessionKey instance, Span<byte> buffer) => instance.Write(buffer);
    }
}
