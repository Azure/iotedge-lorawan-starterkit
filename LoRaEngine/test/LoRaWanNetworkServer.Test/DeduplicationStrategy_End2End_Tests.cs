// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class DeduplicationStrategy_End2End_Tests : MessageProcessorMultipleGatewayTest
    {
        private readonly DeduplicationStrategyFactory factory;
        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

        public DeduplicationStrategy_End2End_Tests()
        {
            this.factory = new DeduplicationStrategyFactory(this.LoRaDeviceApi.Object);
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
        }

        [Theory]
        [InlineData(DeduplicationMode.Mark)]
        [InlineData(DeduplicationMode.Drop)]
        [InlineData(DeduplicationMode.None)]
        public async Task Validate_Dup_Message_Processing(DeduplicationMode mode)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            bool messageProcessed = mode == DeduplicationMode.Drop;
            messageProcessed = false;

            this.LoRaDeviceApi.Setup(x => x.ExecuteFunctionBundlerAsync(simulatedDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                .Returns<string, FunctionBundlerRequest>((dev, req) =>
                {
                    var isDup = messageProcessed;
                    messageProcessed = true;
                    return Task.FromResult<FunctionBundlerResult>(new FunctionBundlerResult()
                    {
                        DeduplicationResult = new DeduplicationResult
                        {
                            IsDuplicate = isDup
                        }
                    });
                });

            this.LoRaDeviceApi
                .Setup(x => x.NextFCntDownAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                .ReturnsAsync((uint)(simulatedDevice.FrmCntDown + 1))
                .Callback(() =>
                {
                    // this call should only be made, if we do not have a deduplication strategy
                    // since otherwise we expect the fcntDown to be calculated in the same API call as deduplication
                    Assert.True(mode == DeduplicationMode.None);
                });

            this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);

            var shouldBeMarked = false;

            this.LoRaDeviceClient
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

            this.LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            await this.SendTwoMessages(mode);
        }

        private async Task SendTwoMessages(DeduplicationMode mode)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.Deduplication = mode;

            var loRaDeviceRegistry1 = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);
            var loRaDeviceRegistry2 = new LoRaDeviceRegistry(this.SecondServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor1 = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry1,
                this.FrameCounterUpdateStrategyProvider);

            var messageProcessor2 = new MessageDispatcher(
                this.SecondServerConfiguration,
                loRaDeviceRegistry2,
                this.SecondFrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var request1 = this.CreateWaitableRequest(rxpk);
            var request2 = this.CreateWaitableRequest(rxpk);

            messageProcessor1.DispatchRequest(request1);

            _ = Task.Run(() =>
            {
                messageProcessor2.DispatchRequest(request2);
            });

            await Task.WhenAll(request1.WaitCompleteAsync(Timeout.Infinite), request2.WaitCompleteAsync(Timeout.Infinite));

            switch (mode)
            {
                case DeduplicationMode.Drop:
                    Assert.True(request1.ProcessingSucceeded);
                    Assert.True(request2.ProcessingFailed);
                    Assert.Equal<LoRaDeviceRequestFailedReason>(LoRaDeviceRequestFailedReason.DeduplicationDrop, request2.ProcessingFailedReason);
                    break;
                case DeduplicationMode.Mark:
                    Assert.True(request1.ProcessingSucceeded);
                    Assert.True(request2.ProcessingSucceeded);
                    break;
                case DeduplicationMode.None:
                    break;
            }
        }
    }
}
