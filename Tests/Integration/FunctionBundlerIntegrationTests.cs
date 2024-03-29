// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class FunctionBundlerIntegrationTests : MessageProcessorTestBase
    {
        public FunctionBundlerIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Fact]
        public async Task Validate_Function_Bundler_Execution()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var devEUI = simulatedDevice.LoRaDevice.DevEui;

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

            LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<DevEui>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);

            LoRaDeviceClient
                .Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loRaDevice);
            await using var loRaDeviceRegistry1 = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            await using var messageProcessor1 = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                loRaDeviceRegistry1,
                FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: 1);

            using var request = CreateWaitableRequest(payload);

            messageProcessor1.DispatchRequest(request);

            Assert.True(await request.WaitCompleteAsync());

            LoRaDeviceApi.VerifyAll();
        }
    }
}
