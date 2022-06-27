// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using Newtonsoft.Json;
    using Xunit;

    public sealed class PreferredGatewayResultTests
    {
        [Fact]
        public void Can_Deserialize()
        {
            // arrange
            var input = new global::LoraKeysManagerFacade.PreferredGatewayResult(12, new global::LoraKeysManagerFacade.LoRaDevicePreferredGateway("gateway", 13));

            // act
            var result = JsonConvert.DeserializeObject<LoRaWan.NetworkServer.PreferredGatewayResult>(JsonConvert.SerializeObject(input));

            // assert
            Assert.Equal(input.RequestFcntUp, result!.RequestFcntUp);
            Assert.Equal(input.PreferredGatewayID, result.PreferredGatewayID);
            Assert.Equal(input.Conflict, result.Conflict);
            Assert.Equal(input.CurrentFcntUp, result.CurrentFcntUp);
            Assert.Equal(input.ErrorMessage, result.ErrorMessage);
        }
    }
}
