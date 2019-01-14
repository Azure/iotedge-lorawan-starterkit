using LoRaTools.LoRaMessage;
using LoRaTools.Utils;
using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Collections.Generic;
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

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(actual);
        }


        [Fact]
        public async Task When_Device_Is_Found_In_Api_Should_Update_Twin_And_Return()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: ServerConfiguration.GatewayID));
            simulatedDevice.LoRaDevice.NwkSKey = string.Empty;
            simulatedDevice.LoRaDevice.AppSKey = string.Empty;
            var joinRequest = simulatedDevice.CreateJoinRequest();


            var loRaDeviceClient = new Mock<ILoRaDeviceClient>();
            var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, loRaDeviceClient.Object);
            loRaDevice.SetFcntDown(10);
            loRaDevice.SetFcntUp(20);

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];

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

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.NotNull(actual);

            var pktFwdMessage = actual.GetPktFwdMessage();
            Assert.NotNull(pktFwdMessage.Txpk);
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(pktFwdMessage.Txpk.data), loRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ByteArray(loRaDevice.DevAddr).ToArray());
            
            // Device properties were set with the computes values of the join operation
            Assert.Equal(joinAccept.AppNonce.ToArray(), ReversedByteArray(loRaDevice.AppNonce).ToArray());
            Assert.NotEmpty(loRaDevice.NwkSKey);
            Assert.NotEmpty(loRaDevice.AppSKey);
            Assert.True(loRaDevice.IsOurDevice);

            // Device frame counts were reset
            Assert.Equal(0, loRaDevice.FCntDown);
            Assert.Equal(0, loRaDevice.FCntUp);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // Twin property were updated
            loRaDeviceClient.VerifyAll();          
        }

        private Memory<byte> ReversedByteArray(string value) 
        {
            var array = LoRaTools.Utils.ConversionHelper.StringToByteArray(value);
            
            Array.Reverse(array);
            return array;
        }

        Memory<byte> ByteArray(string value) =>  LoRaTools.Utils.ConversionHelper.StringToByteArray(value);


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

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);
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

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);
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

            var actual = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.Null(actual);


            // Device frame counts did not changed
            Assert.Equal(10, loRaDevice.FCntDown);
            Assert.Equal(20, loRaDevice.FCntUp);
        }


        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Getting_Device_Information_From_Twin_Returns_JoinAccept(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var rxpk = joinRequest.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];

            var payloadDecoder = new Mock<ILoRaPayloadDecoder>();

            var devNonce = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoinAppSKey = null;
            string afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((updatedTwin) => {
                    afterJoinAppSKey = updatedTwin[TwinProperty.AppSKey];
                    afterJoinNwkSKey = updatedTwin[TwinProperty.NwkSKey];
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                })
                .ReturnsAsync(true);
            

            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchDevicesAsync(ServerConfiguration.GatewayID, null, devEUI, appEUI, devNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));            

            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);


            // Setup frame counter strategy for FrameCounterLoRaDeviceInitializer
            if (deviceGatewayID == null)
            {
                this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetMultiGatewayStrategy())
                    .Returns(FrameCounterUpdateStrategy.Object);
            }
            else
            {
                this.FrameCounterUpdateStrategyFactory.Setup(x => x.GetSingleGatewayStrategy())
                    .Returns(FrameCounterUpdateStrategy.Object);
            }

            // Send to message processor
            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyFactory.Object,
                payloadDecoder.Object
                );

            var downlinkMessage = await messageProcessor.ProcessMessageAsync(rxpk);
            Assert.NotNull(downlinkMessage);
            var joinAccept = new LoRaPayloadJoinAccept(Convert.FromBase64String(downlinkMessage.txpk.data), simulatedDevice.LoRaDevice.AppKey);
            Assert.Equal(joinAccept.DevAddr.ToArray(), ConversionHelper.StringToByteArray(afterJoinDevAddr));            

            // check that the device is in cache
            Assert.True(memoryCache.TryGetValue<DevEUIToLoRaDeviceDictionary>(afterJoinDevAddr, out var cachedDevices));
            Assert.True(cachedDevices.TryGetValue(devEUI, out var cachedDevice));
            Assert.Equal(afterJoinAppSKey, cachedDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, cachedDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, cachedDevice.DevAddr);
            Assert.True(cachedDevice.IsOurDevice);
            if (deviceGatewayID == null)
                Assert.Null(cachedDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, cachedDevice.GatewayID);

            // fcnt is restarted
            Assert.Equal(0, cachedDevice.FCntUp);
            Assert.Equal(0, cachedDevice.FCntDown);
            Assert.False(cachedDevice.HasFrameCountChanges);
        }
    }
}
