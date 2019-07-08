// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// Multiple gateway message processor tests
    /// </summary>
    public class MessageProcessorMultipleGatewayTest : MessageProcessorTestBase
    {
        const string SecondServerGatewayID = "second-gateway";

        public NetworkServerConfiguration SecondServerConfiguration { get; }

        public TestPacketForwarder SecondPacketForwarder { get; }

        public Mock<LoRaDeviceAPIServiceBase> SecondLoRaDeviceApi { get; }

        public LoRaDeviceFrameCounterUpdateStrategyProvider SecondFrameCounterUpdateStrategyProvider { get; }

        private DefaultLoRaDataRequestHandler secondRequestHandlerImplementation;

        public Mock<ILoRaDeviceClient> SecondLoRaDeviceClient { get; }

        public LoRaDeviceClientConnectionManager SecondConnectionManager { get; }

        internal TestLoRaDeviceFactory SecondLoRaDeviceFactory { get; }

        LoRaDevice CreateSecondLoRaDevice(SimulatedDevice simulatedDevice) => TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.SecondLoRaDeviceClient.Object, this.secondRequestHandlerImplementation);

        public MessageProcessorMultipleGatewayTest()
        {
            this.SecondServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = SecondServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(),
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
            this.SecondConnectionManager = new LoRaDeviceClientConnectionManager(new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) }));
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
            var loRaDevice2 = this.CreateSecondLoRaDevice(simulatedDevice);

            var loRaDeviceRegistry1 = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice1), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);
            var loRaDeviceRegistry2 = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loRaDevice2), this.SecondLoRaDeviceApi.Object, this.SecondLoRaDeviceFactory);

            // Send to message processor
            var messageProcessor1 = new MessageDispatcher(
                this.ServerConfiguration,
                loRaDeviceRegistry1,
                this.FrameCounterUpdateStrategyProvider);

            var messageProcessor2 = new MessageDispatcher(
                this.SecondServerConfiguration,
                loRaDeviceRegistry2,
                this.SecondFrameCounterUpdateStrategyProvider);

            // Starts with fcnt up zero
            Assert.Equal(0U, loRaDevice1.FCntUp);
            Assert.Equal(0U, loRaDevice2.FCntUp);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request1 = this.CreateWaitableRequest(rxpk);
            var request2 = this.CreateWaitableRequest(rxpk, this.SecondPacketForwarder);
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
        }
    }
}