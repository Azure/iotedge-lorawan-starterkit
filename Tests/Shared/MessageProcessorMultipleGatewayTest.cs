// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Shared
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
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
            this.SecondServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = SecondServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(CultureInfo.InvariantCulture),
            };

            this.SecondPacketForwarder = new TestPacketForwarder();
            this.SecondLoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.SecondFrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(SecondServerGatewayID, this.SecondLoRaDeviceApi.Object);
            var deduplicationStrategyFactory = new DeduplicationStrategyFactory(this.SecondLoRaDeviceApi.Object);
            var loRaAdrManagerFactory = new LoRAADRManagerFactory(this.SecondLoRaDeviceApi.Object);
            var adrStrategyProvider = new LoRaADRStrategyProvider();
            var functionBundlerProvider = new FunctionBundlerProvider(this.SecondLoRaDeviceApi.Object);
            this.secondRequestHandlerImplementation = new DefaultLoRaDataRequestHandler(this.SecondServerConfiguration, this.SecondFrameCounterUpdateStrategyProvider, new LoRaPayloadDecoder(), deduplicationStrategyFactory, adrStrategyProvider, loRaAdrManagerFactory, functionBundlerProvider);
            this.SecondLoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });
            this.SecondConnectionManager = new LoRaDeviceClientConnectionManager(cache);
            this.SecondLoRaDeviceFactory = new TestLoRaDeviceFactory(this.SecondServerConfiguration, this.SecondFrameCounterUpdateStrategyProvider, this.SecondLoRaDeviceClient.Object, deduplicationStrategyFactory, adrStrategyProvider, loRaAdrManagerFactory, functionBundlerProvider, this.SecondConnectionManager);
        }

        [Fact]
        public async Task Multi_OTAA_Unconfirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            // 1 messages will be sent
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);
            this.SecondLoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);
            this.SecondLoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(It.IsNotNull<string>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                .ReturnsAsync(true);

            // cloud to device messages will be checked twice
            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null)
                .ReturnsAsync((Message)null);

            this.SecondLoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null)
                .ReturnsAsync((Message)null);

            var loRaDevice1 = this.CreateLoRaDevice(simulatedDevice);
            using var connectionManager2 = new SingleDeviceConnectionManager(this.SecondLoRaDeviceClient.Object);
            var loRaDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager2, this.secondRequestHandlerImplementation);

            using var cache1 = this.NewNonEmptyCache(loRaDevice1);
            using var loRaDeviceRegistry1 = new LoRaDeviceRegistry(this.ServerConfiguration, cache1, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);
            using var cache2 = this.NewNonEmptyCache(loRaDevice2);
            using var loRaDeviceRegistry2 = new LoRaDeviceRegistry(this.ServerConfiguration, cache2, this.SecondLoRaDeviceApi.Object, this.SecondLoRaDeviceFactory);

            // Send to message processor
            using var messageProcessor1 = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry1,
                this.FrameCounterUpdateStrategyProvider);

            using var messageProcessor2 = new MessageDispatcher(
                this.SecondServerConfiguration,
                loRaDeviceRegistry2,
                this.SecondFrameCounterUpdateStrategyProvider);

            // Starts with fcnt up zero
            Assert.Equal(0U, loRaDevice1.FCntUp);
            Assert.Equal(0U, loRaDevice2.FCntUp);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            using var request1 = this.CreateWaitableRequest(rxpk);
            using var request2 = this.CreateWaitableRequest(rxpk, this.SecondPacketForwarder);
            messageProcessor1.DispatchRequest(request1);
            messageProcessor2.DispatchRequest(request2);

            await Task.WhenAll(request1.WaitCompleteAsync(), request2.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.SecondLoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            this.SecondLoRaDeviceApi.VerifyAll();
            this.LoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null), Times.Once());
            this.SecondLoRaDeviceClient.Verify(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null), Times.Once());

            // 2. Return is null (there is nothing to send downstream)
            Assert.Null(request1.ResponseDownlink);
            Assert.Null(request2.ResponseDownlink);

            // 3. Frame counter up was updated to 1
            Assert.Equal(1U, loRaDevice1.FCntUp);
            Assert.Equal(1U, loRaDevice2.FCntUp);

            this.SecondLoRaDeviceClient.Setup(ldc => ldc.Dispose());
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
