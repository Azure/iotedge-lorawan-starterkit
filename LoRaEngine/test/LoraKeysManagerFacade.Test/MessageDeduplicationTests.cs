// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using Microsoft.Azure.WebJobs;
    using Xunit;

    public class MessageDeduplicationTests : FunctionTestBase
    {
        public MessageDeduplicationTests()
        {
            LoRaDeviceCache.EnsureCacheStore(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public void MessageDeduplication_Duplicates_Found()
        {
            string gateway1Id = NewUniqueEUI64();
            string gateway2Id = NewUniqueEUI64();
            string dev1EUI = NewUniqueEUI64();

            var result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev1EUI, gateway1Id, 1, 1, string.Empty);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev1EUI, gateway2Id, 1, 1, string.Empty);
            Assert.True(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);
        }

        [Fact]
        public void MessageDeduplication_Resubmit_Allowed()
        {
            string gateway1Id = NewUniqueEUI64();
            string dev1EUI = NewUniqueEUI64();

            var result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev1EUI, gateway1Id, 1, 1, string.Empty);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev1EUI, gateway1Id, 1, 1, string.Empty);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);
        }

        [Fact]
        public void MessageDeduplication_DifferentDevices_Allowed()
        {
            string gateway1Id = NewUniqueEUI64();
            string gateway2Id = NewUniqueEUI64();
            string dev1EUI = NewUniqueEUI64();
            string dev2EUI = NewUniqueEUI64();

            var result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev1EUI, gateway1Id, 1, 1, string.Empty);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev1EUI, gateway2Id, 2, 1, string.Empty);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway2Id, result.GatewayId);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev2EUI, gateway1Id, 1, 1, string.Empty);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway1Id, result.GatewayId);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(dev2EUI, gateway2Id, 2, 1, string.Empty);
            Assert.False(result.IsDuplicate);
            Assert.Equal(gateway2Id, result.GatewayId);
        }
    }
}
