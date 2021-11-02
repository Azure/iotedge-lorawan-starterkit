// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using System;
    using Xunit;
    using static LoRaWan.NetworkServer.ConcentratorDeduplication;

    public sealed class ConcentratorDeduplicationTest : IDisposable
    {
        private readonly ConcentratorDeduplication ConcentratorDeduplication;

        public ConcentratorDeduplicationTest()
        {
            this.ConcentratorDeduplication = new ConcentratorDeduplication();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void When_Device_Is_Not_Encountered_Should_Not_Find_Duplicates(bool isCacheEmpty)
        {
            var loraRequest = new LoRaRequest(default, default, default, "a_concentrator");
            if (!isCacheEmpty)
            {
                _ = this.ConcentratorDeduplication.IsDuplicate(loraRequest, 123, isRestartedDevice: false, "another_device");
            }

            var result = this.ConcentratorDeduplication.IsDuplicate(loraRequest, 5, isRestartedDevice: false, "a_device");
            Assert.False(result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_Device_Is_Restarted_Should_Update_Cache(bool isRestartedDevice)
        {
            var loraRequest = new LoRaRequest(default, default, default, "a_concentrator");
            var deviceEUI = "a_device";
            uint previousFrameCounter = 123;
            uint newFrameCounter = 1;
            _ = this.ConcentratorDeduplication.IsDuplicate(loraRequest, previousFrameCounter, false, deviceEUI);

            _ = this.ConcentratorDeduplication.IsDuplicate(loraRequest, newFrameCounter, isRestartedDevice, deviceEUI);
            _ = this.ConcentratorDeduplication.Cache.TryGetValue(deviceEUI, out var value);
            var cachedFrameCounter = ((DeviceEUICacheEntry)value).FrameCounter;

            if (isRestartedDevice)
                Assert.Equal(newFrameCounter, cachedFrameCounter);
            else
                Assert.Equal(previousFrameCounter, cachedFrameCounter);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        public void When_Cache_Contains_Message_From_The_Same_Concentrator_Result_Does_Not_Depend_On_FrameCounter(uint frameCounterReceived, bool expectedResult)
        {
            var loraRequest = new LoRaRequest(default, default, default, "a_concentrator");
            var deviceEUI = "a_device";
            // add a message to the cache with a frame counter in the middle of what will be tested
            _ = this.ConcentratorDeduplication.IsDuplicate(loraRequest, 1, isRestartedDevice: false, deviceEUI);

            var result = this.ConcentratorDeduplication.IsDuplicate(loraRequest, frameCounterReceived, isRestartedDevice: false, deviceEUI);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public void When_Cache_Contains_Message_From_Different_Concentrator_Result_Depends_On_FrameCounter(uint frameCounterReceived, bool expectedResult)
        {
            var loraRequest = new LoRaRequest(default, default, default, "a_concentrator");
            var deviceEUI = "a_device";
            // add a message to the cache with a frame counter in the middle of what will be tested
            _ = this.ConcentratorDeduplication.IsDuplicate(loraRequest, 1, isRestartedDevice: false, deviceEUI);

            var loraRequestFromDifferentConcentrator = new LoRaRequest(default, default, default, "another_concentrator");
            var result = this.ConcentratorDeduplication.IsDuplicate(loraRequestFromDifferentConcentrator, frameCounterReceived, isRestartedDevice: false, deviceEUI);
            Assert.Equal(expectedResult, result);
        }

        public void Dispose() => this.ConcentratorDeduplication.Dispose();
    }
}
