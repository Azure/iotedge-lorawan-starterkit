// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    /// <summary>
    /// Multiple gateway message processor tests
    /// </summary>
    public class MessageProcessorMultipleGatewayTest : MessageProcessorTestBase
    {
        private readonly Mock<ILoRaDeviceRegistry> loRaDeviceRegistry2;

        public MessageProcessorMultipleGatewayTest()
        {
            this.loRaDeviceRegistry2 = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            this.loRaDeviceRegistry2.Setup(x => x.RegisterDeviceInitializer(It.IsAny<ILoRaDeviceInitializer>()));
        }

        [Fact]
        public async Task Multi_OTAA_Unconfirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // 2 messages will be sent
            loraDeviceClient.SetupSequence(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true)
                .ReturnsAsync(true);

            // cloud to device messages will be checked twice
            loraDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null)
                .ReturnsAsync((Message)null);

            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);
            var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice1);

            this.loRaDeviceRegistry2.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice2);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetMultiGatewayStrategy())
                .Returns(new TestMultiGatewayUpdateFrameStrategy());

            // Frame counter will be asked to save changes
            this.FrameCounterUpdateStrategy.Setup(x => x.SaveChangesAsync(loraDevice1)).ReturnsAsync(true);
            this.FrameCounterUpdateStrategy.Setup(x => x.SaveChangesAsync(loraDevice2)).ReturnsAsync(true);

            // Send to message processor
            var messageProcessor1 = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object);

            var messageProcessor2 = new MessageProcessor(
                new NetworkServerConfiguration() { GatewayID = "test-gateway-2" },
                this.loRaDeviceRegistry2.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object);

            // Starts with fcnt up zero
            Assert.Equal(0, loraDevice1.FCntUp);
            Assert.Equal(0, loraDevice2.FCntUp);

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];

            var t1 = messageProcessor1.ProcessMessageAsync(rxpk);
            var t2 = messageProcessor2.ProcessMessageAsync(rxpk);

            await Task.WhenAll(t1, t2);

            // Expectations
            // 1. Message was sent to IoT Hub twice
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is null (there is nothing to send downstream)
            Assert.Null(t1.Result);
            Assert.Null(t2.Result);

            // 4. Frame counter up was updated to 1
            Assert.Equal(1, loraDevice1.FCntUp);
            Assert.Equal(1, loraDevice2.FCntUp);
        }
    }
}