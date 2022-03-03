// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoraKeysManagerFacade.FunctionBundler;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Moq;
    using Xunit;

    public class MessageDeduplicationTests : FunctionTestBase
    {
        private readonly DeduplicationExecutionItem deduplicationExecutionItem;
        private readonly Mock<IServiceClient> serviceClientMock;

        public MessageDeduplicationTests()
        {
            this.serviceClientMock = new Mock<IServiceClient>();
            this.deduplicationExecutionItem = new DeduplicationExecutionItem(new LoRaInMemoryDeviceStore(), this.serviceClientMock.Object, TestMeter.Instance);
        }

        [Fact]
        public async Task MessageDeduplication_Duplicates_Found()
        {
            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();
            var dev1EUI = TestEui.GenerateDevEui();

            var result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway2Id, 1, 1);
            Assert.True(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);
        }

        [Fact]
        public async Task MessageDeduplication_Resubmit_Allowed()
        {
            var gateway1Id = NewUniqueEUI64();
            var dev1EUI = TestEui.GenerateDevEui();

            var result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);
        }

        [Fact]
        public async Task MessageDeduplication_DifferentDevices_Allowed()
        {
            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();
            var dev1EUI = TestEui.GenerateDevEui();
            var dev2EUI = TestEui.GenerateDevEui();

            this.serviceClientMock.Setup(x => x.InvokeDeviceMethodAsync(
                It.IsAny<string>(), LoraKeysManagerFacadeConstants.NetworkServerModuleId, It.IsAny<CloudToDeviceMethod>()))
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = 200 });

            var result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway2Id, 2, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway2Id, result.GatewayId);

            // Make sure direct method was invoked on the LNS module to notify gateway 1
            // that it is no longer the owning gateway for device 1
            this.serviceClientMock.Verify(
                x => x.InvokeDeviceMethodAsync(gateway1Id.ToString(), LoraKeysManagerFacadeConstants.NetworkServerModuleId,
                It.Is<CloudToDeviceMethod>(
                    m => m.MethodName == LoraKeysManagerFacadeConstants.CloudToDeviceCloseConnection
                    && m.GetPayloadAsJson().Contains(dev1EUI.ToString()))),
                Times.Once);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev2EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev2EUI, gateway2Id, 2, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway2Id, result.GatewayId);

            // Make sure direct method was invoked for gateway 1 and device 2
            this.serviceClientMock.Verify(
                x => x.InvokeDeviceMethodAsync(gateway1Id.ToString(), LoraKeysManagerFacadeConstants.NetworkServerModuleId,
                It.Is<CloudToDeviceMethod>(
                    m => m.MethodName == LoraKeysManagerFacadeConstants.CloudToDeviceCloseConnection
                    && m.GetPayloadAsJson().Contains(dev2EUI.ToString()))),
                Times.Once);
        }

        [Fact]
        public async Task MessageDeduplication_When_Direct_Method_Throws_Does_Not_Throw()
        {
            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();
            var dev1EUI = TestEui.GenerateDevEui();

            this.serviceClientMock.Setup(x => x.InvokeDeviceMethodAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CloudToDeviceMethod>()))
                .ThrowsAsync(new IotHubException("Failed to invoke direct method"));

            var result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway2Id, 2, 1);

            this.serviceClientMock.Verify(
                x => x.InvokeDeviceMethodAsync(gateway1Id.ToString(), LoraKeysManagerFacadeConstants.NetworkServerModuleId,
                It.Is<CloudToDeviceMethod>(m => m.MethodName == LoraKeysManagerFacadeConstants.CloudToDeviceCloseConnection)),
                Times.Once);

            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway2Id, result.GatewayId);
        }
    }
}
