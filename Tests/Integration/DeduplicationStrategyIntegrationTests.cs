// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    public class DeduplicationStrategyIntegrationTests : MessageProcessorMultipleGatewayBase
    {

        [Theory]
        [InlineData(DeduplicationMode.Mark)]
        [InlineData(DeduplicationMode.Drop)]
        [InlineData(DeduplicationMode.None)]
        public async Task Validate_Dup_Message_Processing(DeduplicationMode mode)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var messageProcessed = mode == DeduplicationMode.Drop;
            messageProcessed = false;

            LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simulatedDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                .Returns<string, FunctionBundlerRequest>((dev, req) =>
                {
                    var isDup = messageProcessed;
                    messageProcessed = true;
                    return Task.FromResult(new FunctionBundlerResult()
                    {
                        DeduplicationResult = new DeduplicationResult
                        {
                            IsDuplicate = isDup
                        }
                    });
                });

            LoRaDeviceApi
                .Setup(x => x.NextFCntDownAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                .ReturnsAsync(simulatedDevice.FrmCntDown + 1)
                .Callback(() =>
                {
                    // this call should only be made, if we do not have a deduplication strategy
                    // since otherwise we expect the fcntDown to be calculated in the same API call as deduplication
                    Assert.True(mode == DeduplicationMode.None);
                });

            LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);

            var shouldBeMarked = false;

            LoRaDeviceClient
                .Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true)
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((telemetry, dict) =>
                {
                    if (shouldBeMarked)
                    {
                        Assert.True(telemetry.DupMsg);
                    }
                    else
                    {
                        Assert.Null(telemetry.DupMsg);
                    }

                    shouldBeMarked = mode == DeduplicationMode.Mark;
                });

            LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            await SendTwoUnconfirmedFirstMessages(mode);
        }

        private async Task SendTwoUnconfirmedFirstMessages(DeduplicationMode mode)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.Deduplication = mode;

            using var cache1 = EmptyMemoryCache();
            using var loraDeviceCache1 = CreateDeviceCache(loRaDevice);
            using var loRaDeviceRegistry1 = new LoRaDeviceRegistry(ServerConfiguration, cache1, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache1);
            using var cache2 = EmptyMemoryCache();
            using var loraDeviceCache2 = CreateDeviceCache(loRaDevice);
            using var loRaDeviceRegistry2 = new LoRaDeviceRegistry(SecondServerConfiguration, cache2, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache2);

            using var messageProcessor1 = new MessageDispatcher(
                ServerConfiguration,
                loRaDeviceRegistry1,
                FrameCounterUpdateStrategyProvider);

            using var messageProcessor2 = new MessageDispatcher(
                SecondServerConfiguration,
                loRaDeviceRegistry2,
                SecondFrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            using var request1 = CreateWaitableRequest(rxpk);
            using var request2 = CreateWaitableRequest(rxpk);

            messageProcessor1.DispatchRequest(request1);
            messageProcessor2.DispatchRequest(request2);

            await Task.WhenAll(request1.WaitCompleteAsync(Timeout.Infinite), request2.WaitCompleteAsync(Timeout.Infinite));

            switch (mode)
            {
                case DeduplicationMode.Drop:
                    Assert.True(request1.ProcessingSucceeded);
                    Assert.True(request2.ProcessingFailed);
                    Assert.Equal(LoRaDeviceRequestFailedReason.DeduplicationDrop, request2.ProcessingFailedReason);
                    break;
                case DeduplicationMode.Mark:
                    Assert.True(request1.ProcessingSucceeded);
                    Assert.True(request2.ProcessingFailed);
                    break;
                case DeduplicationMode.None:
                default:
                    Assert.True(request1.ProcessingSucceeded);
                    Assert.True(request2.ProcessingFailed);
                    break;
            }
        }
    }
}
