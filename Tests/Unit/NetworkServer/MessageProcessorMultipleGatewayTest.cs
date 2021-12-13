// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    public class MessageProcessorMultipleGatewayTest : MessageProcessorMultipleGatewayBase
    {
        public MessageProcessorMultipleGatewayTest() : base()
        { }

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

            using var loRaDevice1 = CreateLoRaDevice(simulatedDevice);
            using var connectionManager2 = new SingleDeviceConnectionManager(SecondLoRaDeviceClient.Object);
            using var loRaDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager2, SecondRequestHandlerImplementation);

            using var cache1 = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loRaDevice1);
            using var loRaDeviceRegistry1 = new LoRaDeviceRegistry(ServerConfiguration, cache1, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);
            using var cache2 = EmptyMemoryCache();
            using var loraDeviceCache2 = CreateDeviceCache(loRaDevice2);
            using var loRaDeviceRegistry2 = new LoRaDeviceRegistry(ServerConfiguration, cache2, SecondLoRaDeviceApi.Object, SecondLoRaDeviceFactory, loraDeviceCache2);

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
            using var request1 = CreateWaitableRequest(rxpk, PacketForwarder);
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

            // the following setup is required after VerifyAll() is called
            LoRaDeviceClient.Setup(ldc => ldc.Dispose());
            SecondLoRaDeviceClient.Setup(ldc => ldc.Dispose());
        }
    }
}
