using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaWan.NetworkServer;
using LoRaWan.Test.Shared;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWanNetworkServer.Test
{
    /// <summary>
    /// Summary description for Class1
    /// </summary>
    public class MessageProcessor2Test
    {
        private long startTime;
        private readonly Rxpk rxpk;
        private readonly byte[] macAddress;

        public MessageProcessor2Test()
        {
            this.startTime = DateTimeOffset.Now.Ticks;
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
        public async Task Unconfirmed_Message_Should_Send_Data_To_IotHub_And_Return_Null()
        {
            var device = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = device.CreateUnconfirmedDataUpMessage("1234");


            // Create Rxpk
            var rxpk = CreateRxpk(payload);

            var loraDevice = new Mock<ILoRaDevice>();
            var deviceRegistry = new Mock<ILoRaDeviceRegistry>();
            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            deviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(() => loraDevice.Object);

            // Send to message processor
            var messageProcessor = new MessageProcessor2(
                deviceRegistry.Object,
                new LoRaDeviceFrameCounterUpdateStrategy("testGateway"),
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessLoraMessage(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub

            // 2. Return is null (there is nothing to send downstream)
            Assert.Null(actual);
        }
    }
}