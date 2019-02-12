// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Newtonsoft.Json;
    using Xunit;

    public class LoRaPayloadDecoderTest
    {
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
            var target = new LoRaPayloadDecoder();

            var result = await target.DecodeMessageAsync("12", Encoding.UTF8.GetBytes(value), 1, "DecoderValueSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal(expectedJson, json);
        }

        [Fact]
        public async Task When_Payload_Is_Null_DecoderValueSensor_Should_Return_Empty()
        {
            var target = new LoRaPayloadDecoder();

            var result = await target.DecodeMessageAsync("12", null, 1, "DecoderValueSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal("{\"value\":\"\"}", json);
        }

        [Fact]
        public async Task When_Payload_Is_Empty_DecoderValueSensor_Should_Return_Empty()
        {
            var target = new LoRaPayloadDecoder();

            var result = await target.DecodeMessageAsync("12", new byte[0], 1, "DecoderValueSensor");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal("{\"value\":\"\"}", json);
        }

        [Fact]
        public async Task When_Payload_Is_Null_ExternalDecoder_Should_Be_Called_With_Empty_Payload()
        {
            var devEUI = "12";
            byte fport = 8;
            var decodedValue = "{\"from\":\"http\"}";
            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                var queryDictionary = System.Web.HttpUtility.ParseQueryString(r.RequestUri.Query);
                Assert.Equal(devEUI, queryDictionary.Get("devEUI"));
                Assert.Equal(fport.ToString(), queryDictionary.Get("fport"));
                Assert.Empty(queryDictionary.Get("payload"));

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(decodedValue, Encoding.UTF8, "application/json"),
                };
            });

            var target = new LoRaPayloadDecoder(new HttpClient(httpMessageHandler));
            var result = await target.DecodeMessageAsync(devEUI, null, fport, "http://test/decoder");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal(decodedValue, json);
        }

        [Fact]
        public async Task When_Payload_Is_Empty_ExternalDecoder_Should_Be_Called_With_Empty_Payload()
        {
            var devEUI = "12";
            byte fport = 8;
            var decodedValue = "{\"from\":\"http\"}";
            var httpMessageHandler = new HttpMessageHandlerMock();
            httpMessageHandler.SetupHandler((r) =>
            {
                var queryDictionary = System.Web.HttpUtility.ParseQueryString(r.RequestUri.Query);
                Assert.Equal(devEUI, queryDictionary.Get("devEUI"));
                Assert.Equal(fport.ToString(), queryDictionary.Get("fport"));
                Assert.Empty(queryDictionary.Get("payload"));

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(decodedValue, Encoding.UTF8, "application/json"),
                };
            });

            var target = new LoRaPayloadDecoder(new HttpClient(httpMessageHandler));
            var result = await target.DecodeMessageAsync(devEUI, new byte[0], fport, "http://test/decoder");
            var json = JsonConvert.SerializeObject(result.GetDecodedPayload());
            Assert.Equal(decodedValue, json);
        }

        [Theory]
        [InlineData("DecoderValueSensor", "1234", 1234L, 1)]
        [InlineData("DecoderValueSensor", "1234", 1234L, 2)]
        [InlineData("DECODERVALUESENSOR", "1234", 1234L, 1)]
        [InlineData("decodervaluesensor", "1234", 1234L, 2)]
        [InlineData("DECODERVALUESENSOR", "12.34", 12.34, 1)]
        [InlineData("decodervaluesensor", "-12.34", -12.34, 2)]
        [InlineData("decodervaluesensor", "hello world", "hello world", 2)]
        [InlineData("decodervaluesensor", " 1 ", 1L, 2)]
        [InlineData("decodervaluesensor", "$1", "$1", 2)]
        [InlineData("decodervaluesensor", "100'000", "100'000", 2)]
        public async Task When_Decoder_Is_DecoderValueSensor_Return_In_Value(string decoder, string payloadString, object expectedValue, byte fport)
        {
            var payload = Encoding.UTF8.GetBytes(payloadString);

            var target = new LoRaPayloadDecoder();
            var actual = await target.DecodeMessageAsync("12", payload, fport, decoder);
            Assert.IsType<DecodedPayloadValue>(actual.Value);
            var decodedPayloadValue = (DecodedPayloadValue)actual.Value;
            Assert.Equal(expectedValue, decodedPayloadValue.Value);
        }

        [Theory]
        [InlineData(null, "1234", 1)]
        [InlineData(null, "1234", 2)]
        [InlineData(null, "helloworld", 1)]
        [InlineData(null, "helloworld", 2)]
        [InlineData("", "1234", 1)]
        [InlineData("", "1234", 2)]
        [InlineData("", "helloworld", 1)]
        [InlineData("", "helloworld", 2)]
        [InlineData("Does not exist", "1234", 1)]
        [InlineData("Does not exist", "1234", 2)]
        [InlineData("Does not exist", "helloworld", 1)]
        [InlineData("Does not exist", "helloworld", 2)]
        public async Task When_Decoder_Is_Undefined_Return_In_Value(string decoder, string payloadString, byte fport)
        {
            var payload = Encoding.UTF8.GetBytes(payloadString);

            var target = new LoRaPayloadDecoder();
            var actual = await target.DecodeMessageAsync("12", payload, fport, decoder);
            Assert.NotNull(actual.Error);
        }
    }
}