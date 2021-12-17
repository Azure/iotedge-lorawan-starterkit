// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Newtonsoft.Json;
    using Xunit;

    public class LoRaPayloadDecoderTest
    {
        private const FramePort TestPort1 = (FramePort)1;
        private const FramePort TestPort2 = (FramePort)2;
        private const FramePort TestPort8 = (FramePort)8;

        [Theory]
        [InlineData(10L, "{\"value\":10}")]
        [InlineData(10.01, "{\"value\":10.01}")]
        [InlineData(-10.01, "{\"value\":-10.01}")]
        [InlineData(-10L, "{\"value\":-10}")]
        [InlineData(0.1, "{\"value\":0.1}")]
        [InlineData(0L, "{\"value\":0}")]
        public void When_Value_Is_Numeric_Json_Should_Not_Be_Quoted(object value, string expectedJson)
        {
            var target = new DecodePayloadResult(value);
            var json = JsonConvert.SerializeObject(target);
            Assert.Equal(expectedJson, json);

            var parsed = JsonConvert.DeserializeObject<DecodePayloadResult>(json);
            Assert.IsType(value.GetType(), parsed.Value);
        }

        [Theory]
        [InlineData("hello", "{\"value\":\"hello\"}")]
        [InlineData("abc", "{\"value\":\"abc\"}")]
        [InlineData("", "{\"value\":\"\"}")]
        [InlineData("100'000", "{\"value\":\"100'000\"}")]
        public void When_Value_Is_NAN_Json_Should_Be_Quoted(object value, string expectedJson)
        {
            var target = new DecodePayloadResult(value);
            var json = JsonConvert.SerializeObject(target);
            Assert.Equal(expectedJson, json);
        }

        [Theory]
        [InlineData("10", "{\"value\":10}")]
        [InlineData("10.01", "{\"value\":10.01}")]
        [InlineData("-10.01", "{\"value\":-10.01}")]
        [InlineData("-10", "{\"value\":-10}")]
        [InlineData("0.1", "{\"value\":0.1}")]
        [InlineData("0", "{\"value\":0}")]
        [InlineData("helloworld", "{\"value\":\"helloworld\"}")]
        [InlineData("100'000", "{\"value\":\"100'000\"}")]
        [InlineData("AE0198", "{\"value\":\"AE0198\"}")]
        [InlineData("0xAE0198", "{\"value\":\"0xAE0198\"}")]
        public async Task When_Value_From_String_Is_Passed_Should_Try_To_Validate_As_Number(string value, string expectedJson)
        {
            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);

            var result = await target.DecodeMessageAsync("12", Encoding.UTF8.GetBytes(value), TestPort1, "DecoderValueSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal(expectedJson, json);
        }

        [Fact]
        public async Task When_Payload_Is_Null_DecoderValueSensor_Should_Return_Empty()
        {
            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);

            var result = await target.DecodeMessageAsync("12", null, TestPort1, "DecoderValueSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal("{\"value\":\"\"}", json);
        }

        [Fact]
        public async Task When_Payload_Is_Empty_DecoderValueSensor_Should_Return_Empty()
        {
            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);

            var result = await target.DecodeMessageAsync("12", Array.Empty<byte>(), TestPort1, "DecoderValueSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal("{\"value\":\"\"}", json);
        }

        [Fact]
        public async Task When_Payload_Is_Null_DecoderHexSensor_Should_Return_Empty()
        {
            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);

            var result = await target.DecodeMessageAsync("12", null, TestPort1, "DecoderHexSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal("{\"value\":\"\"}", json);
        }

        [Fact]
        public async Task When_Payload_Is_Empty_DecoderHexSensor_Should_Return_Empty()
        {
            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);

            var result = await target.DecodeMessageAsync("12", Array.Empty<byte>(), TestPort1, "DecoderHexSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal("{\"value\":\"\"}", json);
        }

        [Fact]
        public async Task When_Payload_Is_Null_ExternalDecoder_Should_Be_Called_With_Empty_Payload()
        {
            var devEUI = "12";
            var fport = TestPort8;
            var decodedValue = "{\"from\":\"http\"}";
            using var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                var queryDictionary = System.Web.HttpUtility.ParseQueryString(r.RequestUri.Query);
                Assert.Equal(devEUI, queryDictionary.Get("devEUI"));
                Assert.Equal(((byte)fport).ToString(CultureInfo.InvariantCulture), queryDictionary.Get("fport"));
                Assert.Empty(queryDictionary.Get("payload"));

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(decodedValue, Encoding.UTF8, "application/json"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            var target = new LoRaPayloadDecoder(httpClient);
            var result = await target.DecodeMessageAsync(devEUI, null, fport, "http://test/decoder");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal(decodedValue, json);
        }

        [Fact]
        public async Task When_Payload_Is_Empty_ExternalDecoder_Should_Be_Called_With_Empty_Payload()
        {
            var devEUI = "12";
            var fport = TestPort8;
            var decodedValue = "{\"from\":\"http\"}";
            using var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                var queryDictionary = System.Web.HttpUtility.ParseQueryString(r.RequestUri.Query);
                Assert.Equal(devEUI, queryDictionary.Get("devEUI"));
                Assert.Equal(((byte)fport).ToString(CultureInfo.InvariantCulture), queryDictionary.Get("fport"));
                Assert.Empty(queryDictionary.Get("payload"));

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(decodedValue, Encoding.UTF8, "application/json"),
                };
            });

            using var httpClient = new HttpClient(httpMessageHandler);
            var target = new LoRaPayloadDecoder(httpClient);
            var result = await target.DecodeMessageAsync(devEUI, Array.Empty<byte>(), fport, "http://test/decoder");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal(decodedValue, json);
        }

        [Theory]
        [InlineData("DecoderValueSensor", "1234", 1234L, TestPort1)]
        [InlineData("DecoderValueSensor", "1234", 1234L, TestPort2)]
        [InlineData("DECODERVALUESENSOR", "1234", 1234L, TestPort1)]
        [InlineData("decodervaluesensor", "1234", 1234L, TestPort2)]
        [InlineData("DECODERVALUESENSOR", "12.34", 12.34, TestPort1)]
        [InlineData("decodervaluesensor", "-12.34", -12.34, TestPort2)]
        [InlineData("decodervaluesensor", "hello world", "hello world", TestPort2)]
        [InlineData("decodervaluesensor", " 1 ", 1L, TestPort2)]
        [InlineData("decodervaluesensor", "$1", "$1", TestPort2)]
        [InlineData("decodervaluesensor", "100'000", "100'000", TestPort2)]
        public async Task When_Decoder_Is_DecoderValueSensor_Return_In_Value(string decoder, string payloadString, object expectedValue, FramePort fport)
        {
            var payload = Encoding.UTF8.GetBytes(payloadString);

            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);
            var actual = await target.DecodeMessageAsync("12", payload, fport, decoder);
            Assert.IsType<DecodedPayloadValue>(actual.Value);
            var decodedPayloadValue = (DecodedPayloadValue)actual.Value;
            Assert.Equal(expectedValue, decodedPayloadValue.Value);
        }

        [Theory]
        [InlineData("DecoderHexSensor", "1234", "31323334", TestPort1)]
        [InlineData("DecoderHexSensor", "1234", "31323334", TestPort2)]
        [InlineData("DECODERHEXSENSOR", "1234", "31323334", TestPort1)]
        [InlineData("decoderhexsensor", "1234", "31323334", TestPort2)]
        [InlineData("DECODERHEXSENSOR", "12.34", "31322E3334", TestPort1)]
        [InlineData("decoderhexsensor", "-12.34", "2D31322E3334", TestPort2)]
        [InlineData("decoderhexsensor", "hello world", "68656C6C6F20776F726C64", TestPort2)]
        [InlineData("decoderhexsensor", " 1 ", "203120", TestPort2)]
        [InlineData("decoderhexsensor", "$1", "2431", TestPort2)]
        [InlineData("decoderhexsensor", "100'000", "31303027303030", TestPort2)]
        public async Task When_Decoder_Is_DecoderHexSensor_Return_In_Value(string decoder, string payloadString, object expectedValue, FramePort fport)
        {
            var payload = Encoding.UTF8.GetBytes(payloadString);

            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);
            var actual = await target.DecodeMessageAsync("12", payload, fport, decoder);
            Assert.IsType<DecodedPayloadValue>(actual.Value);
            var decodedPayloadValue = (DecodedPayloadValue)actual.Value;
            Assert.Equal(expectedValue, decodedPayloadValue.Value);
        }

        [Theory]
        [InlineData(null, "1234", TestPort1)]
        [InlineData(null, "1234", TestPort2)]
        [InlineData(null, "helloworld", TestPort1)]
        [InlineData(null, "helloworld", TestPort2)]
        [InlineData("", "1234", TestPort1)]
        [InlineData("", "1234", TestPort2)]
        [InlineData("", "helloworld", TestPort1)]
        [InlineData("", "helloworld", TestPort2)]
        [InlineData("Does not exist", "1234", TestPort1)]
        [InlineData("Does not exist", "1234", TestPort2)]
        [InlineData("Does not exist", "helloworld", TestPort1)]
        [InlineData("Does not exist", "helloworld", TestPort2)]
        public async Task When_Decoder_Is_Undefined_Return_In_Value(string decoder, string payloadString, FramePort fport)
        {
            var payload = Encoding.UTF8.GetBytes(payloadString);

            var target = new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance);
            var actual = await target.DecodeMessageAsync("12", payload, fport, decoder);
            Assert.NotNull(actual.Error);
        }
    }
}
