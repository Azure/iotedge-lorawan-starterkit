// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using Microsoft.Azure.WebJobs;
    using Xunit;

    public class MessageDeduplicationTests
    {
        const string Gateway1Id = "Gateway1";
        const string Gateway2Id = "Gateway2";
        const string Dev1EUI = "Dev1";
        const string Dev2EUI = "Dev2";

        [Fact]
        public void MessageDeduplication_Duplicates_Found()
        {
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

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
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

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
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

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
            var store = new LoRaInMemoryDeviceStore();
            LoRaDeviceCache.InitCacheStore(store);

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
