//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaTools.LoRaMessage;
using LoRaTools.LoRaPhysical;
using LoRaTools.Regions;
using LoRaWan.NetworkServer;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Moq;
using System;
using System.Runtime.InteropServices;
using System.Text;
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
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(() => null);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

           var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Returns null
            Assert.Null(actual);
        }

        [Fact]
        public async Task Unknown_Region_Should_Return_Null()
        {
            // Setup
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];
            rxpk.freq = 0;
            
            var payloadDecoder = new Mock<ILoRaPayloadDecoder>(MockBehavior.Strict);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

           var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Returns null
            Assert.Null(actual);
        }

        [Fact]
        public async Task ABP_Unconfirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 10);
            simulatedDevice.FrmCntUp = 9;

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];

            var loraDeviceClient = new Mock<ILoRaDeviceClient>();
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(this.FrameCounterUpdateStrategy.Object);

            // Frame counter will be asked to save changes
            this.FrameCounterUpdateStrategy.Setup(x => x.SaveChangesAsync(loraDevice)).ReturnsAsync(true);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is null (there is nothing to send downstream)
            Assert.Null(actual);

            // 4. Frame counter up was updated
            Assert.Equal(10, loraDevice.FCntUp);
        }

        [Fact]
        public async Task OTAA_Confirmed_Message_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_DownstreamMessage()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID));
            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234");

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];

            var loraDeviceClient = new Mock<ILoRaDeviceClient>();
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(new SingleGatewayFrameCounterUpdateStrategy());

            // Frame counter will be asked to save changes
            this.FrameCounterUpdateStrategy.Setup(x => x.SaveChangesAsync(loraDevice)).ReturnsAsync(true);

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(actual);
            Assert.IsType<DownlinkPktFwdMessage>(actual);
            var downlinkMessage = (DownlinkPktFwdMessage)actual;
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.txpk.data));
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            

            // 4. Frame counter up was updated
            Assert.Equal(1, loraDevice.FCntUp);

            // 5. Frame counter down was incremented
            Assert.Equal(1, loraDevice.FCntDown);
            Assert.Equal(1, MemoryMarshal.Read<UInt16>(payloadDataDown.Fcnt.Span));

        }

        [Fact]
        public async Task OTAA_Unconfirmed_Message_With_FcntUp_10_Should_Send_Data_To_IotHub_Update_FcntUp_And_Return_Null()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            loraDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(new SingleGatewayFrameCounterUpdateStrategy());

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return nothing
            Assert.Null(actual);
           
            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is not changed
            Assert.Equal(InitialDeviceFcntDown, loraDevice.FCntDown);

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task OTAA_Unconfirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message()
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: PayloadFcnt);

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var cloudToDeviceMessage = new Message(Encoding.UTF8.GetBytes("c2d"));
            cloudToDeviceMessage.Properties[MessageProcessor.FPORT_MSG_PROPERTY_KEY] = "1";
            loraDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            loraDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(new SingleGatewayFrameCounterUpdateStrategy());

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is downstream message
            Assert.NotNull(actual);
            Assert.IsType<DownlinkPktFwdMessage>(actual);
            var downlinkMessage = (DownlinkPktFwdMessage)actual;
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.txpk.data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
            
            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, MemoryMarshal.Read<UInt16>(payloadDataDown.Fcnt.Span));

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }


        bool IsTwinFcntZero(TwinCollection t) => (int)t[TwinProperty.FCntDown] == 0 && (int)t[TwinProperty.FCntUp] == 0;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task When_ABP_Device_With_Relaxed_FrameCounter_Has_FCntUP_Zero_Or_One_Should_Reset_Counter_And_Process_Message(int payloadFCnt)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID));

            // generate payload with frame count 0 or 1
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFCnt);

            simulatedDevice.FrmCntDown = 0;
            simulatedDevice.FrmCntUp = 10;

            // Create Rxpk
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).rxpk[0];

            var loraDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var loraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loraDeviceClient.Object);

            // Will send the event to IoT Hub
            loraDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            // will try to get C2D message
            loraDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>())).ReturnsAsync((Message)null);

            // Will save the fcnt up/down to zero
            loraDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.Is<TwinCollection>((t) => IsTwinFcntZero(t))));

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            this.LoRaDeviceRegistry.Setup(x => x.GetDeviceForPayloadAsync(It.IsAny<LoRaTools.LoRaMessage.LoRaPayloadData>()))
                .ReturnsAsync(loraDevice);

            // Setup frame counter strategy
            this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                .Returns(new SingleGatewayFrameCounterUpdateStrategy());

            // Send to message processor
            var messageProcessor = new MessageProcessor(
                this.ServerConfiguration,
                this.LoRaDeviceRegistry.Object,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);

            // Expectations
            // 1. Message was sent to IoT Hub
            loraDeviceClient.VerifyAll();

            // 2. Single gateway frame counter strategy was used
            this.FrameCounterUpdateStrategyFactory.VerifyAll();

            // 3. Return is null (there is nothing to send downstream)
            Assert.Null(actual);

            // 4. Frame counter up was updated
            Assert.Equal(payloadFCnt, loraDevice.FCntUp);
        }
    }
}