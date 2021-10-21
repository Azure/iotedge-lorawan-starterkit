// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class MessageDeduplicationTests : FunctionTestBase
    {
        private readonly DeduplicationExecutionItem deduplicationExecutionItem;

        public MessageDeduplicationTests()
        {
            this.deduplicationExecutionItem = new DeduplicationExecutionItem(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public async Task MessageDeduplication_Duplicates_Found()
        {
            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();
            var dev1EUI = NewUniqueEUI64();

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
            var dev1EUI = NewUniqueEUI64();

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
            var dev1EUI = NewUniqueEUI64();
            var dev2EUI = NewUniqueEUI64();

            var result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev1EUI, gateway2Id, 2, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway2Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev2EUI, gateway1Id, 1, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = await this.deduplicationExecutionItem.GetDuplicateMessageResultAsync(dev2EUI, gateway2Id, 2, 1);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway2Id, result.GatewayId);
        }
    }
}
