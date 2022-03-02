// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System.Collections.Generic;
    using global::LoRaTools;
    using Newtonsoft.Json;
    using Xunit;

    public class MacCommandJsonConverterTest
    {
        [Fact]
        public void When_Serializing_List_Should_Create_Correct_Items()
        {
            var input = @"[ { ""cid"": ""DutyCycleCmd"", ""dutyCyclePL"": 12 }, { ""cid"": ""DevStatusCmd"" } ]";
            var list = JsonConvert.DeserializeObject<List<MacCommand>>(input);
            Assert.Equal(2, list.Count);
            _ = Assert.IsType<DutyCycleRequest>(list[0]);
            _ = Assert.IsType<DevStatusRequest>(list[1]);

            var dutyCycleCmd = (DutyCycleRequest)list[0];
            Assert.Equal(12, dutyCycleCmd.DutyCyclePL);
        }

        [Fact]
        public void When_Serializing_Single_Object_Should_Create_Correct_Items()
        {
            var input = @"{ ""cid"": ""DutyCycleCmd"", ""dutyCyclePL"": 12 }";
            var genericMACCommand = JsonConvert.DeserializeObject<MacCommand>(input);
            Assert.NotNull(genericMACCommand);
            _ = Assert.IsType<DutyCycleRequest>(genericMACCommand);
            var dutyCycleCmd = (DutyCycleRequest)genericMACCommand;
            Assert.Equal(12, dutyCycleCmd.DutyCyclePL);
        }

        [Theory]
        [InlineData(@"{ ""cid"": ""LinkAdrCmd"", ""datarate"": 2, ""txpower"": 4, ""chmask"": 25, ""chmaskcntl"": 0, ""nbrep"": 1 }")]
        [InlineData(@"{ ""cid"": ""LinkAdrCmd"", ""dataRate"": 2, ""txPower"": 4, ""chMask"": 25, ""chMaskCntl"": 0, ""nbRep"": 1 }")]
        [InlineData(@"{ ""cid"": ""LinkAdrCmd"", ""DataRate"": 2, ""TXPower"": 4, ""ChMask"": 25, ""ChMaskCntl"": 0, ""NbRep"": 1 }")]
        public void When_Serializing_LinkAdrCmd_Should_Create_Correct_Items(string input)
        {
            var macCommand = JsonConvert.DeserializeObject<MacCommand>(input);
            Assert.NotNull(macCommand);
            Assert.IsType<LinkADRRequest>(macCommand);
            var linkADRCmd = (LinkADRRequest)macCommand;
            Assert.Equal(DataRateIndex.DR2, linkADRCmd.DataRate);
            Assert.Equal(4, linkADRCmd.TxPower);
            Assert.Equal(25, linkADRCmd.ChMask);
            Assert.Equal(0, linkADRCmd.ChMaskCntl);
            Assert.Equal(1, linkADRCmd.NbRep);
        }

        [Theory]
        [InlineData(@"{ ""cid"": ""LinkAdrCmd"", ""datarate"": 2, ""chmask"": 25, ""chmaskcntl"": 0, ""nbrep"": 1 }", "txPower")]
        [InlineData(@"{ ""cid"": ""LinkAdrCmd"", ""chmask"": 20 }", "dataRate")]
        [InlineData(@"{ ""cid"": ""LinkAdrCmd"", ""datarate"": 6, ""txpower"": 4, ""chmask"": 0 }", "chMaskCntl")]
        [InlineData(@"{ ""cid"": ""LinkAdrCmd"", ""datarate"": 8, ""txpower"": 0, ""chmask"": 20, ""chmaskcntl"": 1 }", "nbRep")]
        public void When_Serializing_Invalid_LinkAdrCmd_Should_Throw(string input, string missingProperty)
        {
            var ex = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<MacCommand>(input));
            Assert.Equal($"Property '{missingProperty}' is missing", ex.Message);
        }

        [Theory]
        [InlineData(Cid.TxParamSetupCmd)]
        public void When_Serializing_Invalid_Cid_Should_Throw(Cid cid)
        {
            var ex = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<MacCommand>(@$"{{""cid"":{(int)cid}}}"));
            Assert.Equal($"Unhandled command identifier: {cid}", ex.Message);
        }
    }
}
