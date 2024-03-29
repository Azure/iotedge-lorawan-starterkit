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
    using global::LoRaTools.Regions;
    using LoRaWan.Tests.Common;

    public class TwinCollectionExtensionsTests
    {
        private readonly ILogger logger;

        public TwinCollectionExtensionsTests()
        {
            this.logger = NullLogger.Instance;
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

            var tc = CreateTwinCollectionReader(key, expectedValue);
            Assert.Equal(expectedValue, tc.ReadRequiredString(key));
        }

        [Fact]
        public void ReadRequired_Success()
        {
            const string key = "test";
            var expectedValue = new StationEui(1);

            var tc = CreateTwinCollectionReader(key, expectedValue.ToString());
            Assert.Equal(expectedValue, tc.ReadRequired<StationEui>(key));
        }

        [Fact]
        public void ReadRequired_Nullable_Success()
        {
            const string key = "test";
            var expectedValue = new StationEui(1);

            var tc = CreateTwinCollectionReader(key, expectedValue.ToString());
            Assert.Equal(expectedValue, tc.ReadRequired<StationEui?>(key));
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(false, null)]
        [InlineData(true, "")]
        [InlineData(true, "1234")]
        public void ReadRequired_Throws_If_TryParse_Fails(bool add, string val)
        {
            const string key = "test";
            var tc = new TwinCollection();
            var reader = new TwinCollectionReader(tc, this.logger);
            if (add)
                tc[key] = val;

            Assert.Throws<InvalidOperationException>(() => reader.ReadRequired<StationEui>(key));
        }

        [Fact]
        public void TryRead_Returns_False_When_Key_Does_Not_Exist()
        {
            var tc = new TwinCollectionReader(new TwinCollection(), this.logger);
            Assert.False(tc.TryRead<string>("invalid", out _));
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
            var tc = CreateTwinCollectionReader(key, jsonValue);
            Assert.True(tc.TryRead<DataRateIndex>(key, out var dr));
            Assert.Equal(expectedValue, dr);
        }

        [Fact]
        public void Custom_Reader_DevNonce_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(new DevNonce(5));

        [Fact]
        public void Custom_Reader_StationEui_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(new StationEui(ulong.MaxValue));

        [Fact]
        public void Custom_Reader_DevAddr_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(new DevAddr(5, 100));

        [Fact]
        public void Custom_Reader_AppKey_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(TestKeys.CreateAppKey(5));

        [Fact]
        public void Custom_Reader_AppKey_Nullable_Succeeds() =>
            Custom_Reader_Succeeds_For_Type((AppKey?)TestKeys.CreateAppKey(5));

        [Fact]
        public void Custom_Reader_AppSessionKey_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(TestKeys.CreateAppSessionKey(5));

        [Fact]
        public void Custom_Reader_AppSessionKey_Nullable_Succeeds() =>
            Custom_Reader_Succeeds_For_Type((AppSessionKey?)TestKeys.CreateAppSessionKey(5));

        [Fact]
        public void Custom_Reader_NetworkSessionKey_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(TestKeys.CreateNetworkSessionKey(5));

        [Fact]
        public void Custom_Reader_NetworkSessionKey_Nullable_Succeeds() =>
            Custom_Reader_Succeeds_For_Type((NetworkSessionKey?)TestKeys.CreateNetworkSessionKey(5));

        [Fact]
        public void Custom_Reader_JoinEui_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(new JoinEui(5));

        [Fact]
        public void Custom_Reader_NetId_Succeeds() =>
            Custom_Reader_Succeeds_For_Type(new NetId(5));

        private void Custom_Reader_Succeeds_For_Type<T>(T expected) where T : notnull
        {
            const string key = "foo";
            var tc = CreateTwinCollectionReader(key, expected.ToString());
            Assert.True(tc.TryRead(key, out T result));
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Reading_Json_Configuration_Block_Suceeds()
        {
            const string key = "config";
            const string rssiValue = "\"rssi\": 1";
            const string jsonValue = "{" + rssiValue + "}";

            var tc = new TwinCollection($"{{'{key}':{jsonValue}}}");

            Assert.True(tc.TryReadJsonBlock(key, out var json));
            Assert.Contains(rssiValue, json, StringComparison.Ordinal);
        }

        [Fact]
        public void Parsing_Json_Configuration_Block_Suceeds()
        {
            const string key = "tx";
            var jsonValue = $"\"{nameof(DwellTimeSetting.DownlinkDwellTime)}\": true, " +
                            $"\"{nameof(DwellTimeSetting.UplinkDwellTime)}\": false, " +
                            $"\"{nameof(DwellTimeSetting.MaxEirp)}\": 5, ";

            jsonValue = string.Concat("{", jsonValue, "}");

            var tc = new TwinCollection($"{{'{key}':{jsonValue}}}");

            Assert.True(tc.TryParseJson<DwellTimeSetting>(key, this.logger, out var parsedDt));
            Assert.Equal(new DwellTimeSetting(true, false, 5), parsedDt);
        }

        private TwinCollectionReader CreateTwinCollectionReader(string key, string jsonValue)
            => CreateTwinCollectionReader<string>(key, $"'{jsonValue}'");

        private TwinCollectionReader CreateTwinCollectionReader<T>(string key, T jsonValue)
            => new TwinCollectionReader(new TwinCollection($"{{'{key}':{jsonValue}}}"), this.logger);
    }
}
