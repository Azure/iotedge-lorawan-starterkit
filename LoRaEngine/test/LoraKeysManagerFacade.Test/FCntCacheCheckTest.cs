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
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = FCntCacheCheck.GetNextFCntDown(deviceEUI, gatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);
        }

        [Fact]
        public void FrameCounter_Down_Update_Server()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = FCntCacheCheck.GetNextFCntDown(deviceEUI, gatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(deviceEUI, gatewayId, 2, 1, new ExecutionContext());
            Assert.Equal(3, next);
        }

        [Fact]
        public void FrameCounter_Down_Update_Device()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = FCntCacheCheck.GetNextFCntDown(deviceEUI, gatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(deviceEUI, gatewayId, 3, 10, new ExecutionContext());
            Assert.Equal(11, next);
        }

        [Fact]
        public void FrameCounter_Down_Retry_Increment()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = FCntCacheCheck.GetNextFCntDown(deviceEUI, gatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(deviceEUI, gatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(3, next);
        }
    }
}