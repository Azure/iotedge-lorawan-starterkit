// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class LoRaDeviceFrameCounterUpdateStrategyProviderTest
    {
        private Mock<LoRaDeviceAPIServiceBase> loRaDeviceApi;

        public LoRaDeviceFrameCounterUpdateStrategyProviderTest()
        {
            this.loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void When_Device_Has_No_GatewayID_Should_Return_MultiGateway(string deviceGatewayID)
        {
            var target = new LoRaDeviceFrameCounterUpdateStrategyProvider("test-gateway", this.loRaDeviceApi.Object);
            var actual = target.GetStrategy(deviceGatewayID);
            Assert.NotNull(actual);
            Assert.IsType<MultiGatewayFrameCounterUpdateStrategy>(actual);
        }

        [Theory]
        [InlineData("test-gateway")]
        [InlineData("TEST-GATEWAY")]
        public void When_Device_Has_Matching_GatewayID_Should_Return_SingleGateway(string deviceGatewayID)
        {
            var target = new LoRaDeviceFrameCounterUpdateStrategyProvider("test-gateway", this.loRaDeviceApi.Object);
            var actual = target.GetStrategy(deviceGatewayID);
            Assert.NotNull(actual);
            Assert.IsType<SingleGatewayFrameCounterUpdateStrategy>(actual);
        }

        [Theory]
        [InlineData("test-gateway1")]
        [InlineData("TEST-GATEWAY2")]
        public void When_Device_Has_No_Matching_GatewayID_Should_Return_Null(string deviceGatewayID)
        {
            var target = new LoRaDeviceFrameCounterUpdateStrategyProvider("test-gateway", this.loRaDeviceApi.Object);
            var actual = target.GetStrategy(deviceGatewayID);
            Assert.Null(actual);
        }
    }
}
