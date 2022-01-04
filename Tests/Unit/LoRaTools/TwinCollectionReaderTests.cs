// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using Microsoft.Azure.Devices.Shared;
    using Xunit;
    using System;
    using System.Globalization;
    using global::LoRaTools.Utils;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Logging;

    public class TwinCollectionReaderTests
    {
        private readonly ILogger<TwinCollectionReader> logger;

        public TwinCollectionReaderTests()
        {
            this.logger = NullLogger<TwinCollectionReader>.Instance;
        }

        [Fact]
        public void SafeRead_Returns_Default_If_Value_Does_Not_Exist()
        {
            var tc = new TwinCollection();
            var reader = new TwinCollectionReader(tc, this.logger);

            Assert.False(reader.TryRead<string>("test", out var someString));
            Assert.Null(someString);

            Assert.False(reader.TryRead<ushort>("test", out var someUShort));
            Assert.Equal(0, someUShort);

            Assert.False(reader.TryRead<ushort?>("test", out var someUShortNullable));
            Assert.Null(someUShortNullable);
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(false, null)]
        [InlineData(true, "")]
        public void ReadRequiredString_Throws_If_String_Is_Not_Configured(bool add, string val)
        {
            const string key = "test";
            var tc = new TwinCollection();
            var reader = new TwinCollectionReader(tc, this.logger);
            if (add)
                tc[key] = val;

            Assert.Throws<InvalidOperationException>(() => reader.ReadRequiredString(key));
        }

        [Fact]
        public void ReadRequiredString_Reads_Valid_String()
        {
            const string key = "test";
            const string expectedValue = "expected";

            var tc = CreateTwinCollectionReader(key, QuoteJsonString(expectedValue));
            Assert.Equal(expectedValue, tc.ReadRequiredString(key));
        }

        [Fact]
        public void TryRead_Returns_False_When_Key_Does_Not_Exist()
        {
            var tc = new TwinCollectionReader(new TwinCollection(), this.logger);
            Assert.False(tc.TryRead<string>("invalid", out _));
        }

        [Fact]
        public void Null_Values_Are_Returned_As_Null()
        {
            var tc = new TwinCollection();
            const string key = "test";
            tc[key] = null;
            var reader = new TwinCollectionReader(tc, this.logger);
            Assert.Null(reader.SafeRead<string>(key));
            Assert.False(reader.TryRead<string>(key, out _));
        }

        [Theory]
        [InlineData(typeof(string), "test", "'test'")]
        [InlineData(typeof(int),   int.MinValue)]
        [InlineData(typeof(int),   int.MaxValue)]
        [InlineData(typeof(bool),  true, "true")]
        [InlineData(typeof(ulong), ulong.MaxValue)]
        [InlineData(typeof(ushort),ushort.MaxValue)]
        public void TryRead_Returns_True_When_Key_Exists_And_Correct_Value_Is_Returned(Type targetType, object someValue, string jsonValue = null)
        {
            const string key = "test";

            var tr = CreateTwinCollectionReader(key, jsonValue ?? someValue);
            var openMethod = typeof(TwinCollectionReader).GetMethod("TryRead");
            var typedMethod = openMethod.MakeGenericMethod(targetType);
            var parameters = new object[] { key, null };
            Assert.True((bool)typedMethod.Invoke(tr, parameters));
            Assert.Equal(someValue, parameters[^1]);
        }

        [Fact]
        public void TryRead_Overflows_Are_Handled()
        {
            const string key = "test";
            var tc = CreateTwinCollectionReader(key, ulong.MaxValue);
            Assert.False(tc.TryRead<int>(key, out _));
        }

        [Fact]
        public void TryRead_InvalidTypeConversions_Are_Handled()
        {
            const string key = "test";
            var tc = CreateTwinCollectionReader(key, "true");
            Assert.False(tc.TryRead<DateTime>(key, out _));
        }

        [Fact]
        public void TryRead_String_As_Numeric_Should_Succeed()
        {
            const string key = "test";
            const int val = 10;
            var tc = CreateTwinCollectionReader(key, val.ToString(CultureInfo.InvariantCulture));
            Assert.True(tc.TryRead<int>(key, out var n));
            Assert.Equal(val, n);
        }

        [Fact]
        public void TryRead_Can_Convert_Numbers_To_Enum()
        {
            const string key = "test";
            var tc = CreateTwinCollectionReader(key, (int)DataRateIndex.DR8);
            Assert.True(tc.TryRead<DataRateIndex>(key, out var dr));
            Assert.Equal(DataRateIndex.DR8, dr);
        }

        [Fact]
        public void Undefined_Enum_Values_Are_Not_Parsed()
        {
            const string key = "test";
            var tc = CreateTwinCollectionReader(key, 100);
            Assert.False(tc.TryRead<DataRateIndex>(key, out _));
        }

        [Theory]
        [InlineData(nameof(DataRateIndex.DR8), DataRateIndex.DR8)]
        [InlineData("dr8", DataRateIndex.DR8)]
        public void TryRead_Can_Convert_Strings_To_Enum(string jsonValue, DataRateIndex expectedValue)
        {
            const string key = "test";
            var tc = CreateTwinCollectionReader(key, QuoteJsonString(jsonValue));
            Assert.True(tc.TryRead<DataRateIndex>(key, out var dr));
            Assert.Equal(expectedValue, dr);
        }

        private static string QuoteJsonString(string someString)
            => someString != null ? $"'{someString}'" : null;

        private TwinCollectionReader CreateTwinCollectionReader<T>(string key, T jsonValue)
            => new TwinCollectionReader(new TwinCollection($"{{'{key}':{jsonValue}}}"), this.logger);
    }
}
