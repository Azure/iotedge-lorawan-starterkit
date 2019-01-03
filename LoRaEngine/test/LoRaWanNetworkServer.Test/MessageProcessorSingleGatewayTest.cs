using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaTools.Regions;
using LoRaWan.NetworkServer;
using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{
    /// <summary>
    /// Single gateway message processor tests
    /// </summary>
    public class MessageProcessorSingleGatewayTest : MessageProcessorTestBase
    {
        public MessageProcessorSingleGatewayTest()
        {
            
        }
       

        [Fact]
        public async Task Unknown_Device_Should_Return_Null()
        {
            // Setup
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = CreateRxpk(payload);

            var deviceRegistry = new Mock<ILoRaDeviceRegistry>();
            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            deviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(() => null);

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

           var actual = await messageProcessor.ProcessLoRaMessageAsync(rxpk);

            // Expectations
            // 1. Returns null
            Assert.Null(actual);
        }

        [Fact]
        public async Task OTAA_Unconfirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = CreateRxpk(payload);

            var loraDeviceClient = new Mock<ILoRaDeviceClient>();
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object, isABP: false);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<string>(), null))
                .Returns(Task.FromResult(0));

            var deviceRegistry = new Mock<ILoRaDeviceRegistry>();
            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            deviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(this.FrameCounterUpdateStrategy.Object);

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessLoRaMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is null (there is nothing to send downstream)
            Assert.Null(actual);

            // 4. Frame counter up was incremented
            Assert.Equal(1, loraDevice.FCntUp);
        }


        bool IsTwinFcntZero(TwinCollection t) => (int)t[TwinProperty.FCntDown] == 0 && (int)t[TwinProperty.FCntUp] == 0;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task When_ABP_Device_With_Relaxed_FrameCounter_Has_FCntUP_Zero_Or_One_Should_Reset_Counter_And_Process_Message(int payloadFCnt)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            simulatedDevice.FrmCntDown = 0;
            simulatedDevice.FrmCntUp = payloadFCnt;
            // generate payload with frame count 0 or 1
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            simulatedDevice.FrmCntUp = 10;

            // Create Rxpk
            var rxpk = CreateRxpk(payload);

            var loraDeviceClient = new Mock<ILoRaDeviceClient>();
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object, isABP: true);

            // Will send the event to IoT Hub
            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<string>(), null))
                .Returns(Task.FromResult(0));

            // Will save the fcnt up/down to zero
            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.Is<TwinCollection>((t) => IsTwinFcntZero(t))));

            var deviceRegistry = new Mock<ILoRaDeviceRegistry>();
            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            deviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(new SingleGatewayFrameCounterUpdateStrategy());

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessLoRaMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is null (there is nothing to send downstream)
            Assert.Null(actual);

            // 4. Frame counter up was incremented
            Assert.Equal(1, loraDevice.FCntUp);
        }
    }
}