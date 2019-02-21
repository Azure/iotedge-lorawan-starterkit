// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using Microsoft.Azure.WebJobs;
    using Xunit;

    public class MessageDeduplicationTests
    {
        public MessageDeduplicationTests()
        {
            LoRaDeviceCache.EnsureCacheStore(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public void MessageDeduplication_Duplicates_Found()
        {
            const string Gateway1Id = "GWMessageDeduplicationTests1_1";
            const string Gateway2Id = "GWMessageDeduplicationTests2_1";
            const string Dev1EUI = "DevMessageDeduplicationTests1_1";

            var result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway1Id, 1, null, new ExecutionContext());
            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway2Id, 1, null, new ExecutionContext());
            Assert.True(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);
        }

        [Fact]
        public void MessageDeduplication_Resubmit_Allowed()
        {
            const string Gateway1Id = "GWMessageDeduplicationTests1_2";
            const string Dev1EUI = "DevMessageDeduplicationTests1_2";

            var result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway1Id, 1, null, new ExecutionContext());
            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway1Id, 1, null, new ExecutionContext());
            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);
        }

        [Fact]
        public void MessageDeduplication_DifferentDevices_Allowed()
        {
            const string Gateway1Id = "GWMessageDeduplicationTests1_3";
            const string Gateway2Id = "GWMessageDeduplicationTests2_3";
            const string Dev1EUI = "DevMessageDeduplicationTests1_3";
            const string Dev2EUI = "DevMessageDeduplicationTests2_3";

            var result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway1Id, 1, null, new ExecutionContext());
            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway2Id, 2, null, new ExecutionContext());
            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway2Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev2EUI, Gateway1Id, 1, null, new ExecutionContext());
            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev2EUI, Gateway2Id, 2, null, new ExecutionContext());
            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway2Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);
        }

        [Fact]
        public void MessageDeduplication_FrameCounterDown_Invoke()
        {
            const string Gateway1Id = "GWMessageDeduplicationTests1_4";
            const string Gateway2Id = "GWMessageDeduplicationTests2_4";
            const string Dev1EUI = "DevMessageDeduplicationTests1_4";

            var store = new LoRaInMemoryDeviceStore();
            LoRaDeviceCache.EnsureCacheStore(store);

            int fCntDown = 1;
            var result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway1Id, 1, fCntDown, new ExecutionContext());

            Assert.False(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Equal(++fCntDown, result.ClientFCntDown);

            result = DuplicateMsgCacheCheck.GetDuplicateMessageResult(Dev1EUI, Gateway2Id, 1, fCntDown, new ExecutionContext());
            Assert.True(result.IsDuplicate);
            Assert.Equal(Gateway1Id, result.GatewayId);
            Assert.Null(result.ClientFCntDown);
        }
    }
}
