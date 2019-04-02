// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System.Threading.Tasks;
    using Xunit;

    public class FCntCacheCheckTest : FunctionTestBase
    {
        private readonly FCntCacheCheck fcntCheck;

        public FCntCacheCheckTest()
        {
            this.fcntCheck = new FCntCacheCheck(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public async Task FrameCounter_Down_Initial()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = await this.fcntCheck.GetNextFCntDownAsync(deviceEUI, gatewayId, 1, 1);
            Assert.Equal(2U, next);
        }

        [Fact]
        public async Task FrameCounter_Down_Update_Server()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = await this.fcntCheck.GetNextFCntDownAsync(deviceEUI, gatewayId, 1, 1);
            Assert.Equal(2U, next);

            next = await this.fcntCheck.GetNextFCntDownAsync(deviceEUI, gatewayId, 2, 1);
            Assert.Equal(3U, next);
        }

        [Fact]
        public async Task FrameCounter_Down_Update_Device()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = await this.fcntCheck.GetNextFCntDownAsync(deviceEUI, gatewayId, 1, 1);
            Assert.Equal(2U, next);

            next = await this.fcntCheck.GetNextFCntDownAsync(deviceEUI, gatewayId, 3, 10);
            Assert.Equal(11U, next);
        }

        [Fact]
        public async Task FrameCounter_Down_Retry_Increment()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var next = await this.fcntCheck.GetNextFCntDownAsync(deviceEUI, gatewayId, 1, 1);
            Assert.Equal(2U, next);

            next = await this.fcntCheck.GetNextFCntDownAsync(deviceEUI, gatewayId, 1, 1);
            Assert.Equal(3U, next);
        }
    }
}
