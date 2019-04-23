// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    public class FunctionBundler_End2End_Tests : MessageProcessorTestBase
    {
        private readonly DeduplicationStrategyFactory factory;
        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

        public FunctionBundler_End2End_Tests()
        {
            this.factory = new DeduplicationStrategyFactory(this.LoRaDeviceApi.Object);
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
        }

        [Fact]
        public async Task Validate_Function_Bundler_Execution()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            var loRaDevice = this.CreateLoRaDevice(simulatedDevice);
            loRaDevice.Deduplication = DeduplicationMode.Drop;

            this.LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()))
                    .ReturnsAsync(() => new FunctionBundlerResult
                    {
                        AdrResult = new LoRaTools.ADR.LoRaADRResult
                        {
                            CanConfirmToDevice = true,
                            FCntDown = simulatedDevice.FrmCntDown + 1,
                        },
                        DeduplicationResult = new DeduplicationResult(),
                        NextFCntDown = simulatedDevice.FrmCntDown + 1
                    });

            this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);

            this.LoRaDeviceClient
                .Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var loRaDeviceRegistry1 = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor1 = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry1,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var request = this.CreateWaitableRequest(rxpk);

            messageProcessor1.DispatchRequest(request);

            Assert.True(await request.WaitCompleteAsync());

            this.LoRaDeviceApi.VerifyAll();
        }
    }
}
