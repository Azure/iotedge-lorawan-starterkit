//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaWan.NetworkServer;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{

    public class LoRaPayloadDecoderTest
    {

        [Theory]
        [InlineData("DecoderValueSensor", "1234", 1)]
        [InlineData("DecoderValueSensor", "1234", 2)]
        [InlineData("DECODERVALUESENSOR", "1234", 1)]
        [InlineData("decodervaluesensor", "1234", 2)]
        public async Task When_Decoder_Is_DecoderValueSensor_Return_In_Value(string decoder, string payloadString, byte fport)
        {
            var payload = Encoding.UTF8.GetBytes(payloadString);

            var target = new LoRaPayloadDecoder();
            var actual = await target.DecodeMessageAsync(payload, fport, decoder);
            Assert.Equal(payloadString, actual["value"]);
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
            var actual = await target.DecodeMessageAsync(payload, fport, decoder);
            Assert.NotNull(actual["error"]);
            Assert.Equal(Convert.ToBase64String(payload), actual["rawpayload"]);
        }
    }
}