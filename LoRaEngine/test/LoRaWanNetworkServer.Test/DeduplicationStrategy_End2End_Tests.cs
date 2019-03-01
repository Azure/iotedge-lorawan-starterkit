// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
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
        [InlineData(DeduplicationMode.Mark, true)]
        [InlineData(DeduplicationMode.Drop, true)]
        [InlineData(DeduplicationMode.None, true)]
        [InlineData(DeduplicationMode.Mark, false)]
        [InlineData(DeduplicationMode.Drop, false)]
        [InlineData(DeduplicationMode.None, false)]
        public async Task Validate_Dup_Message_Processing(DeduplicationMode mode, bool confirmed)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1), 10, 10);
            bool messageProcessed = mode == DeduplicationMode.Drop;
            messageProcessed = false;
            this.LoRaDeviceApi
                .Setup(x => x.CheckDuplicateMsgAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
                .Returns<string, int, string, int?>((dev, fcntup, gateway, fcntdown) =>
                {
                    var isDup = messageProcessed;
                    messageProcessed = true;
                    Assert.True((confirmed && fcntdown.HasValue) || (!confirmed && !fcntdown.HasValue));
                    return Task.FromResult<DeduplicationResult>(new DeduplicationResult
                    {
                        IsDuplicate = isDup,
                        ClientFCntDown = fcntdown
                    });
                });

            this.LoRaDeviceApi
                .Setup(x => x.NextFCntDownAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((ushort)(simulatedDevice.FrmCntDown + 1))
                .Callback(() =>
                {
                    // this call should only be made, if we do not have a deduplication strategy
                    // since otherwise we expect the fcntDown to be calculated in the same API call as deduplication
                    Assert.True(mode == DeduplicationMode.None);
                });

            var telemetryList = new ConcurrentQueue<LoRaDeviceTelemetry>();

            this.LoRaDeviceClient
                .Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true)
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((telemetry, dict) =>
                {
                    telemetryList.Enqueue(telemetry);
                });

            this.LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            await this.SendTwoMessages(simulatedDevice, mode, confirmed);

            if (mode == DeduplicationMode.Drop)
            {
                // 1x telemetry without dupMsg
                Assert.Single(telemetryList.Where(t => t.DupMsg == null));

                // 1x telemetry overall
                Assert.Single(telemetryList);
            }
            else if (mode == DeduplicationMode.Mark)
            {
                // 1x telemetry without dupMsg
                Assert.Single(telemetryList.Where(t => t.DupMsg == null));

                // 1x telemetry with dupMsg
                Assert.Single(telemetryList.Where(t => t.DupMsg == true));
            }
        }

        private async Task SendTwoMessages(SimulatedDevice simulatedDevice, DeduplicationMode mode, bool confirmed)
        {
            var loRaDevice1 = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice1.Deduplication = mode;

            var loRaDevice2 = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice2.Deduplication = mode;

            var loRaDeviceRegistry1 = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice1), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);
            var loRaDeviceRegistry2 = new LoRaDeviceRegistry(this.SecondServerConfiguration, this.NewNonEmptyCache(loRaDevice2), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor1 = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry1,
                this.FrameCounterUpdateStrategyProvider);

            var messageProcessor2 = new MessageDispatcher(
                this.SecondServerConfiguration,
                loRaDeviceRegistry2,
                this.SecondFrameCounterUpdateStrategyProvider);

            var payload = confirmed ? simulatedDevice.CreateConfirmedDataUpMessage("1234") : simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

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

        [Fact]
        public async Task When_ConfirmedUp_Where_Deduplication_Has_NoFcntDown_Should_Abandon_Message()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: null),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            this.LoRaDeviceApi.Setup(x => x.CheckDuplicateMsgAsync(simDevice.DevEUI, PayloadFcnt, this.ServerConfiguration.GatewayID, InitialDeviceFcntDown))
                .ReturnsAsync(new DeduplicationResult
                {
                    CanProcess = true,
                    GatewayId = "another-gateway",
                    IsDuplicate = true,
                });

            var loRaDevice = this.CreateLoRaDevice(simDevice);
            loRaDevice.Deduplication = DeduplicationMode.Mark;

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var requestRxpk = simDevice.CreateConfirmedMessageUplink("1", fcnt: PayloadFcnt).Rxpk[0];
            var request = this.CreateWaitableRequest(requestRxpk);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.HandledByAnotherGateway, request.ProcessingFailedReason);

            this.LoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.VerifyAll();
        }
    }
}
