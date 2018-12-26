using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaTools.Regions;
using LoRaWan.NetworkServer;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Client;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWanNetworkServer.Test
{
    /// <summary>
    /// Single gateway message processor tests
    /// </summary>
    public class SingleGatewayMessageProcessor2Test
    {
        private long startTime;
        private readonly Rxpk rxpk;
        private readonly byte[] macAddress;
        private string gatewayID;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategy> frameCounterUpdateStrategy;
        private Mock<ILoRaDeviceFrameCounterUpdateStrategyFactory> frameCounterUpdateStrategyFactory;

        public SingleGatewayMessageProcessor2Test()
        {
            this.startTime = DateTimeOffset.UtcNow.Ticks;
            this.rxpk = new Rxpk()
            {
                chan = 7,
                rfch = 1,
                freq = 903.700000,
                stat = 1,
                modu = "LORA",
                datr = "SF10BW125",
                codr = "4/5",
                rssi = -17,
                lsnr = 12.0f
            };

            this.macAddress = Utility.GetMacAddress();
            this.gatewayID = "testGateway";

            this.frameCounterUpdateStrategy = new Mock<ILoRaDeviceFrameCounterUpdateStrategy>();
            this.frameCounterUpdateStrategyFactory = new Mock<ILoRaDeviceFrameCounterUpdateStrategyFactory>();
        }
        

        Rxpk CreateRxpk(LoRaPayloadData loraPayload)
        {            
            var rxpk = new Rxpk()
            {                
                chan = 7,
                rfch = 1,
                freq = 903.700000,
                stat = 1,
                modu = "LORA",
                datr = "SF10BW125",
                codr = "4/5",
                rssi = -17,
                lsnr = 12.0f
            };

            var data = loraPayload.GetByteMessage();
            rxpk.data = Convert.ToBase64String(data);
            rxpk.size = (uint)data.Length;
            // tmst it is time in micro seconds
            var now = DateTimeOffset.UtcNow;
            var tmst = (now.UtcTicks - this.startTime) / (TimeSpan.TicksPerMillisecond / 1000);
            if (tmst >= UInt32.MaxValue)
            {
                tmst = tmst - UInt32.MaxValue;
                this.startTime = now.UtcTicks - tmst;
            }
            rxpk.tmst = Convert.ToUInt32(tmst);

            return rxpk;
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
            var messageProcessor = new MessageProcessor2(
                this.gatewayID,
                deviceRegistry.Object,
                this.frameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var timeWatcher = new LoRaOperationTimeWatcher(Region.EU);
            var actual = await messageProcessor.ProcessLoRaMessage(rxpk, timeWatcher);

            // Expectations
            // 1. Returns null
            Assert.Null(actual);
        }

        [Fact]
        public async Task Unconfirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.gatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = CreateRxpk(payload);

            var loraDevice = new Mock<TestLoRaDeviceAdapter>(simulatedDevice)
            {
                CallBase = true,
            };

            loraDevice.Setup(x => x.SendEventAsync(It.IsNotNull<Message>()))
                .Returns(Task.FromResult(0));

            var deviceRegistry = new Mock<ILoRaDeviceRegistry>();
            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            deviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(() => loraDevice.Object);

            // Setup frame counter strategy
            this.frameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(this.frameCounterUpdateStrategy.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor2(
                this.gatewayID,
                deviceRegistry.Object,
                this.frameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var timeWatcher = new LoRaOperationTimeWatcher(Region.EU);
            var actual = await messageProcessor.ProcessLoRaMessage(rxpk, timeWatcher);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDevice.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.frameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is null (there is nothing to send downstream)
            Assert.Null(actual);

            // 4. Frame counter up was incremented
            Assert.Equal(1, loraDevice.Object.FcntUp);
        }
    }
}