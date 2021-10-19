// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Shared;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    public class FunctionBundlerIntegrationTests : MessageProcessorTestBase
    {
        private readonly DeduplicationStrategyFactory factory;
        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

        public FunctionBundlerIntegrationTests()
        {
            this.factory = new DeduplicationStrategyFactory(LoRaDeviceApi.Object);
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
        }

        [Fact]
        public async Task Validate_Function_Bundler_Execution()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            var loRaDevice = CreateLoRaDevice(simulatedDevice);
            loRaDevice.Deduplication = DeduplicationMode.Drop;

            LoRaDeviceApi
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

            LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);

            LoRaDeviceClient
                .Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = NewNonEmptyCache(loRaDevice);
            using var loRaDeviceRegistry1 = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory);

            using var messageProcessor1 = new MessageDispatcher(
                ServerConfiguration,
                loRaDeviceRegistry1,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            using var request = CreateWaitableRequest(rxpk);

            messageProcessor1.DispatchRequest(request);

            Assert.True(await request.WaitCompleteAsync());

            LoRaDeviceApi.VerifyAll();
        }
    }
}
