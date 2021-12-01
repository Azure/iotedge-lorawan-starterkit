// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Collections.Generic;
    using global::LoRaTools;
    using Newtonsoft.Json;
    using Org.BouncyCastle.Asn1.BC;
    using Xunit;

    public class MacCommandTest
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

        [Fact]
        public void When_Serializing_LinkAdrCmd_Should_Create_Correct_Items()
        {
            var input = @"{ ""cid"": ""LinkAdrCmd"", ""datarate"": 2, ""txpower"": 4, ""chmask"": 25, ""chmaskctl"": 0, ""nbtrans"": 1 }";
            var macCommand = JsonConvert.DeserializeObject<MacCommand>(input);
            Assert.NotNull(macCommand);
            Assert.IsType<LinkADRRequest>(macCommand);
            var linkADRCmd = (LinkADRRequest)macCommand;
            Assert.Equal(2, linkADRCmd.DataRate);
            Assert.Equal(4, linkADRCmd.TxPower);
            Assert.Equal(25, linkADRCmd.ChMask);
            Assert.Equal(0, linkADRCmd.ChMaskCntl);
        }

        [Fact]
        public void When_Serializing_Invalid_LinkAdrCmd_Should_Throw()
        {
            var input = @"{ ""cid"": ""LinkAdrCmd"", ""datarate"": 2, ""chmaskctl"": 0 }";
            var ex = Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<MacCommand>(input));

            var missingProperties = new string[] { "txpower", "chmask", "nbtrans" };
            foreach (var property in missingProperties)
            {
                Assert.Contains($"Property '{property}' is missing", ex.Message, StringComparison.InvariantCulture);
            }
        }
    }
}
