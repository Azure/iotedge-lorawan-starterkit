// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using Microsoft.Azure.WebJobs;
    using Xunit;

    // Ensure tests don't run in parallel since LoRaRegistryManager is shared
    [Collection("LoraKeysManagerFacade.Test")]
    public class FCntCacheCheckTest
    {
        public FCntCacheCheckTest()
        {
            LoRaDeviceCache.EnsureCacheStore(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public void FrameCounter_Down_Initial()
        {
            const string DeviceEUI = "DevFCntCacheCheckTest1_1";
            const string GatewayId = "GwFCntCacheCheckTest1_1";

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);
        }

        [Fact]
        public void FrameCounter_Down_Update_Server()
        {
            const string DeviceEUI = "DevFCntCacheCheckTest1_2";
            const string GatewayId = "GwFCntCacheCheckTest1_2";

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 2, 1, new ExecutionContext());
            Assert.Equal(3, next);
        }

        [Fact]
        public void FrameCounter_Down_Update_Device()
        {
            const string DeviceEUI = "DevFCntCacheCheckTest1_3";
            const string GatewayId = "GwFCntCacheCheckTest1_3";

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 3, 10, new ExecutionContext());
            Assert.Equal(11, next);
        }

        [Fact]
        public void FrameCounter_Down_Retry_Increment()
        {
            const string DeviceEUI = "DevFCntCacheCheckTest1_4";
            const string GatewayId = "GwFCntCacheCheckTest1_4";

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(3, next);
        }
    }
}