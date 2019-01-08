using LoRaTools.LoRaMessage;
using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Shared;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{
    public class MessageProcessorJoinTest : MessageProcessorTestBase
    {
       
        public MessageProcessorJoinTest()
        {
          
        }


        [Fact]
        public async Task When_Device_Is_Not_Found_In_Api_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = CreateRxpk(joinRequest);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => null);

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessJoinRequestAsync(rxpk);
            Assert.Null(actual);

            // TODO: verify what is inside the txpk
        }


        [Fact]
        public async Task When_Device_Is_Found_In_Api_Should_Update_Twin_And_Return()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();


            var loRaDeviceClient = new Mock<ILoRaDeviceClient>();
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            // Create Rxpk
            var rxpk = CreateRxpk(joinRequest);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => loRaDevice);

            this.LoRaDeviceRegistry.Setup(x => x.UpdateDeviceAfterJoin(loRaDevice));

            // Ensure that the device twin was updated
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.Is<TwinCollection>((t) =>
                t.Contains(TwinProperty.DevAddr) && t.Contains(TwinProperty.FCntDown)
            )))
            .ReturnsAsync(true);


            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessJoinRequestAsync(rxpk);
            Assert.NotNull(actual);

            var pktFwdMessage = actual.GetPktFwdMessage();
            Assert.NotNull(pktFwdMessage.Txpk);
            var joinAccept = new LoRaPayloadJoinAccept(LoRaTools.Utils.ConversionHelper.StringToByteArray(pktFwdMessage.Txpk.data), loRaDevice.AppKey);
           

            // Device frame counts were reset
            Assert.Equal(0, loRaDevice.FCntDown);
            Assert.Equal(0, loRaDevice.FCntUp);

            // Twin property were updated
            loRaDeviceClient.VerifyAll();          
        }


        [Fact]
        public async Task When_Api_Takes_Too_Long_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();


            var loRaDeviceClient = new Mock<ILoRaDeviceClient>();
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            // Create Rxpk
            var rxpk = CreateRxpk(joinRequest);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .Callback(() => Thread.Sleep(TimeSpan.FromSeconds(7)))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessJoinRequestAsync(rxpk);
            Assert.Null(actual);


            // Device frame counts were not modified
            Assert.Equal(10, loRaDevice.FCntDown);
            Assert.Equal(20, loRaDevice.FCntUp);
   
            // Twin property were updated
            loRaDeviceClient.VerifyAll();
        }


        [Fact]
        public async Task When_Device_AppEUI_Does_Not_Match_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();
            
            // Create Rxpk
            var rxpk = CreateRxpk(joinRequest);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            simulatedDevice.LoRaDevice.AppEUI = "FFFFFFFFFFFFFFFF";

            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, null);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => loRaDevice);


            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessJoinRequestAsync(rxpk);
            Assert.Null(actual);


            // Device frame counts did not changed
            Assert.Equal(10, loRaDevice.FCntDown);
            Assert.Equal(20, loRaDevice.FCntUp);

        }


        [Fact]
        public async Task When_Device_Has_Different_Gateway_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: "another-gateway"));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = CreateRxpk(joinRequest);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, null);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce))
                .ReturnsAsync(() => loRaDevice);

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessJoinRequestAsync(rxpk);
            Assert.Null(actual);


            // Device frame counts did not changed
            Assert.Equal(10, loRaDevice.FCntDown);
            Assert.Equal(20, loRaDevice.FCntUp);
        }
    }
}
