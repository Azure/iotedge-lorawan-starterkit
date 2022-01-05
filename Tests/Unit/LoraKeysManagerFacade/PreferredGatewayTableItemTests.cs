// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using global::LoraKeysManagerFacade;
    using Xunit;

    public class PreferredGatewayTableItemTests
    {
        [Theory]
        [InlineData(1.2)]
        [InlineData(-1.2)]
        [InlineData(1)]
        public void When_Composing_ToCachedString_CreateFromCachedString_Is_Identity(double rssi)
        {
            // arrange
            const string gatewayId = "foo";
            var initial = new PreferredGatewayTableItem(gatewayId, rssi);

            // act
            var result = PreferredGatewayTableItem.CreateFromCachedString(initial.ToCachedString());

            // assert
            Assert.Equal(initial.GatewayID, result.GatewayID);
            Assert.Equal(initial.Rssi, result.Rssi);
        }
    }
}
