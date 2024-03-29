// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class LoRaDeviceFrameCounterUpdateStrategyProviderTest
    {
        private readonly Mock<LoRaDeviceAPIServiceBase> loRaDeviceApi;
        private readonly NetworkServerConfiguration networkServerConfiguration;

        public LoRaDeviceFrameCounterUpdateStrategyProviderTest()
        {
            this.loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>();
            networkServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = "test-gateway"
            };
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void When_Device_Has_No_GatewayID_Should_Return_MultiGateway(string deviceGatewayID)
        {
            var target = new LoRaDeviceFrameCounterUpdateStrategyProvider(networkServerConfiguration, this.loRaDeviceApi.Object);
            var actual = target.GetStrategy(deviceGatewayID);
            Assert.NotNull(actual);
            Assert.IsType<MultiGatewayFrameCounterUpdateStrategy>(actual);
        }

        [Theory]
        [InlineData("test-gateway")]
        [InlineData("TEST-GATEWAY")]
        public void When_Device_Has_Matching_GatewayID_Should_Return_SingleGateway(string deviceGatewayID)
        {
            var target = new LoRaDeviceFrameCounterUpdateStrategyProvider(networkServerConfiguration, this.loRaDeviceApi.Object);
            var actual = target.GetStrategy(deviceGatewayID);
            Assert.NotNull(actual);
            Assert.IsType<SingleGatewayFrameCounterUpdateStrategy>(actual);
        }

        [Theory]
        [InlineData("test-gateway1")]
        [InlineData("TEST-GATEWAY2")]
        public void When_Device_Has_No_Matching_GatewayID_Should_Return_Null(string deviceGatewayID)
        {
            var target = new LoRaDeviceFrameCounterUpdateStrategyProvider(networkServerConfiguration, this.loRaDeviceApi.Object);
            var actual = target.GetStrategy(deviceGatewayID);
            Assert.Null(actual);
        }
    }
}
