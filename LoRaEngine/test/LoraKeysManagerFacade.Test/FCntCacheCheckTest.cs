// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using Microsoft.Azure.WebJobs;
    using Xunit;

    public class FCntCacheCheckTest
    {
        const string DeviceEUI = "Dev1";
        const string GatewayId = "Gw1";

        [Fact]
        public void FrameCounter_Down_Initial()
        {
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);
        }

        [Fact]
        public void FrameCounter_Down_Update_Server()
        {
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 2, 1, new ExecutionContext());
            Assert.Equal(3, next);
        }

        [Fact]
        public void FrameCounter_Down_Update_Device()
        {
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 3, 10, new ExecutionContext());
            Assert.Equal(11, next);
        }

        [Fact]
        public void FrameCounter_Down_Retry_Increment()
        {
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

            var next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(2, next);

            next = FCntCacheCheck.GetNextFCntDown(DeviceEUI, GatewayId, 1, 1, new ExecutionContext());
            Assert.Equal(3, next);
        }
    }
}
