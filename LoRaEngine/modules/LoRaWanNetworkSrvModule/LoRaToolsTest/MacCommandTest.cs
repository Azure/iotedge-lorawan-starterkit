// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaTools;
    using Newtonsoft.Json;
    using Xunit;

    public class MacCommandTest
    {
        [Fact]
        public void When_Serializing_List_Should_Create_Correct_Items()
        {
            var input = @"[ { ""cid"": ""DutyCycleCmd"", ""dutyCyclePL"": 12 }, { ""cid"": ""DevStatusCmd"" } ]";
            var list = JsonConvert.DeserializeObject<List<MacCommand>>(input);
            Assert.Equal(2, list.Count);
            Assert.IsType<DutyCycleRequest>(list[0]);
            Assert.IsType<DevStatusRequest>(list[1]);

            var dutyCycleCmd = (DutyCycleRequest)list[0];
            Assert.Equal(12, dutyCycleCmd.DutyCyclePL);
        }

        [Fact]
        public void When_Serializing_Single_Object_Should_Create_Correct_Items()
        {
            var input = @"{ ""cid"": ""DutyCycleCmd"", ""dutyCyclePL"": 12 }";
            var genericMACCommand = JsonConvert.DeserializeObject<MacCommand>(input);
            Assert.NotNull(genericMACCommand);
            Assert.IsType<DutyCycleRequest>(genericMACCommand);
            var dutyCycleCmd = (DutyCycleRequest)genericMACCommand;
            Assert.Equal(12, dutyCycleCmd.DutyCyclePL);
        }
    }
}
