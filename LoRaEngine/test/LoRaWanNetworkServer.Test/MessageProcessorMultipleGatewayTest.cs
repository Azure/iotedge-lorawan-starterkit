// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.Tests.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// Multiple gateway message processor tests.
    /// </summary>
    public class MessageProcessorMultipleGatewayTest : MessageProcessorTestBase
    {
        const string SecondServerGatewayID = "second-gateway";

        private readonly MemoryCache cache;

        public NetworkServerConfiguration SecondServerConfiguration { get; }

        public TestPacketForwarder SecondPacketForwarder { get; }

        public Mock<LoRaDeviceAPIServiceBase> SecondLoRaDeviceApi { get; }

        public LoRaDeviceFrameCounterUpdateStrategyProvider SecondFrameCounterUpdateStrategyProvider { get; }

        private readonly DefaultLoRaDataRequestHandler secondRequestHandlerImplementation;
        private bool disposedValue;

        public Mock<ILoRaDeviceClient> SecondLoRaDeviceClient { get; }

        public LoRaDeviceClientConnectionManager SecondConnectionManager { get; }

        internal TestLoRaDeviceFactory SecondLoRaDeviceFactory { get; }

        public MessageProcessorMultipleGatewayTest()
        {
            SecondServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = SecondServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(CultureInfo.InvariantCulture),
            };

            SecondPacketForwarder = new TestPacketForwarder();
            SecondLoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            SecondFrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(SecondServerGatewayID, SecondLoRaDeviceApi.Object);
            var deduplicationStrategyFactory = new DeduplicationStrategyFactory(SecondLoRaDeviceApi.Object);
            var loRaAdrManagerFactory = new LoRAADRManagerFactory(SecondLoRaDeviceApi.Object);
            var adrStrategyProvider = new LoRaADRStrategyProvider();
            var functionBundlerProvider = new FunctionBundlerProvider(SecondLoRaDeviceApi.Object);
            this.secondRequestHandlerImplementation = new DefaultLoRaDataRequestHandler(SecondServerConfiguration, SecondFrameCounterUpdateStrategyProvider, new LoRaPayloadDecoder(), deduplicationStrategyFactory, adrStrategyProvider, loRaAdrManagerFactory, functionBundlerProvider);
            SecondLoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });
            SecondConnectionManager = new LoRaDeviceClientConnectionManager(cache);
            SecondLoRaDeviceFactory = new TestLoRaDeviceFactory(SecondServerConfiguration, SecondFrameCounterUpdateStrategyProvider, SecondLoRaDeviceClient.Object, deduplicationStrategyFactory, adrStrategyProvider, loRaAdrManagerFactory, functionBundlerProvider, SecondConnectionManager);
        }

        [Fact]
        public async Task Multi_OTAA_Unconfirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            // 1 messages will be sent
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);
            SecondLoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);
            SecondLoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);

            // cloud to device messages will be checked twice
            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null)
                .ReturnsAsync((Message)null);

            SecondLoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null)
                .ReturnsAsync((Message)null);

            var loRaDevice1 = CreateLoRaDevice(simulatedDevice);
            using var connectionManager2 = new SingleDeviceConnectionManager(SecondLoRaDeviceClient.Object);
            var loRaDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager2, this.secondRequestHandlerImplementation);

            using var cache1 = NewNonEmptyCache(loRaDevice1);
            using var loRaDeviceRegistry1 = new LoRaDeviceRegistry(ServerConfiguration, cache1, LoRaDeviceApi.Object, LoRaDeviceFactory);
            using var cache2 = NewNonEmptyCache(loRaDevice2);
            using var loRaDeviceRegistry2 = new LoRaDeviceRegistry(ServerConfiguration, cache2, SecondLoRaDeviceApi.Object, SecondLoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor1 = new MessageDispatcher(
                ServerConfiguration,
                loRaDeviceRegistry1,
                FrameCounterUpdateStrategyProvider);

            using var messageProcessor2 = new MessageDispatcher(
                SecondServerConfiguration,
                loRaDeviceRegistry2,
                SecondFrameCounterUpdateStrategyProvider);

            // Starts with fcnt up zero
            Assert.Equal(0U, loRaDevice1.FCntUp);
            Assert.Equal(0U, loRaDevice2.FCntUp);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            using var request1 = CreateWaitableRequest(rxpk);
            using var request2 = CreateWaitableRequest(rxpk, SecondPacketForwarder);
            messageProcessor1.DispatchRequest(request1);
            messageProcessor2.DispatchRequest(request2);

            await Task.WhenAll(request1.WaitCompleteAsync(), request2.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            SecondLoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            SecondLoRaDeviceApi.VerifyAll();
            LoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null), Times.Once());
            SecondLoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null), Times.Once());

            // 2. Return is null (there is nothing to send downstream)
            Assert.Null(request1.ResponseDownlink);
            Assert.Null(request2.ResponseDownlink);

            // 3. Frame counter up was updated to 1
            Assert.Equal(1U, loRaDevice1.FCntUp);
            Assert.Equal(1U, loRaDevice2.FCntUp);

            SecondLoRaDeviceClient.Setup(ldc => ldc.Dispose());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.cache.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
