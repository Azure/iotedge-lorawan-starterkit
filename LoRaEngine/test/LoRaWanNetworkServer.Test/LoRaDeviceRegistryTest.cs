using LoRaTools.LoRaMessage;
using LoRaWan.NetworkServer;
using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.NetworkServer.Test
{
    public class LoRaDeviceRegistryTest
    {
        NetworkServerConfiguration serverConfiguration;
        private MemoryCache cache;
        private readonly Mock<ILoRaDeviceFactory> loraDeviceFactoryMock;
        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

        public LoRaDeviceRegistryTest()
        {
            this.serverConfiguration = new NetworkServerConfiguration();
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.loraDeviceFactoryMock = new Mock<ILoRaDeviceFactory>();
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
        }


        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Not_In_Api_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchDevicesAsync(this.serverConfiguration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult());
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.Verify();
        }


        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Should_Cache_And_Return_It()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            //Assert.True(payload.CheckMic(simulatedDevice.LoRaDevice.NwkSKey)); // why is this failing?

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.serverConfiguration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));


            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(new Twin());


            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(actual, createdLoraDevice);

            // Device was searched by DevAddr
            apiService.Verify();

            // Device was created by factory
            loraDeviceFactoryMock.Verify();

            // ensure device is in cache
            var cachedItem = this.cache.Get<LoRaDeviceRegistry.DevEUIDeviceDictionary>(createdLoraDevice.DevAddr);
            Assert.NotNull(cachedItem);
            Assert.Single(cachedItem);
            Assert.True(cachedItem.TryGetValue(createdLoraDevice.DevEUI, out var actualCachedLoRaDevice));
            Assert.Same(createdLoraDevice, actualCachedLoRaDevice);
        }



        [Fact]
        public async Task When_ABP_Device_Is_Created_Should_Call_Initializers()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.serverConfiguration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));


            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(new Twin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var initializer = new Mock<ILoRaDeviceInitializer>();
            initializer.Setup(x => x.Initialize(createdLoraDevice));

            target.RegisterDeviceInitializer(initializer.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(actual, createdLoraDevice);

            // Device was searched by DevAddr
            apiService.Verify();

            // Device was created by factory
            loraDeviceFactoryMock.Verify();

            // initializer was called
            initializer.VerifyAll();
        }


        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Does_Not_Match_Mic_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            
            var cachedSimulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            cachedSimulatedDevice.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"; // make different than the payload
            cachedSimulatedDevice.LoRaDevice.DeviceID = "0000000000000002";

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.serverConfiguration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(cachedSimulatedDevice, this.loRaDeviceClient.Object);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // Will get device twin
            loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(new Twin());
               
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);
            
            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            loraDeviceFactoryMock.VerifyAll();
        }

        [Fact]
        public void Simulated_LoRaPayload_Payload_Mic_Should_Succeeded()
        {
            var devAddrText = "00000001";
            var appSKey = "00000000000000000000000000000001";
            var nwkSKey = "00000000000000000000000000000001";
            byte fport = 1;
            var data = "1234";
            byte[] mhbr = new byte[] { 0x40 };
            byte[] devAddr = LoRaTools.Utils.ConversionHelper.StringToByteArray(devAddrText);
            Array.Reverse(devAddr);
            byte[] fCtrl = new byte[] { 0x80 };

            byte[] fCnt = new byte[2];
            fCnt[0]++;
            byte[] fopts = null;
            byte[] fPort = new byte[] { fport };           

            byte[] payload = Encoding.UTF8.GetBytes(data);
            Array.Reverse(payload);
            int direction = 0;
            var standardData = new LoRaPayloadData((LoRaPayloadData.MType)mhbr[0], devAddr, fCtrl, fCnt, fopts, fPort, payload, direction);
            // Need to create Fops. If not, then MIC won't be correct
            standardData.Fopts = new byte[0];
            // First encrypt the data
            standardData.PerformEncryption(appSKey); //"0A501524F8EA5FCBF9BDB5AD7D126F75");
            // Now we have the full package, create the MIC
            standardData.SetMic(nwkSKey); //"99D58493D1205B43EFF938F0F66C339E");            

            Assert.True(standardData.CheckMic(nwkSKey));
        }

        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Does_Not_Match_Gateway_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "a_different_one"));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            Assert.True(payload.CheckMic(simulatedDevice.LoRaDevice.NwkSKey));

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.serverConfiguration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(new Twin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            loraDeviceFactoryMock.VerifyAll();
        }


        [Fact]
        public async Task When_Multiple_Devices_With_Same_DevAddr_Are_Cached_Should_Find_Matching_By_Mic()
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: serverConfiguration.GatewayID));
            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, null);

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: serverConfiguration.GatewayID));
            simulatedDevice2.LoRaDevice.DeviceID = "00000002";
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice2, null);

            var existingCache = new LoRaDeviceRegistry.DevEUIDeviceDictionary();
            this.cache.Set<LoRaDeviceRegistry.DevEUIDeviceDictionary>(simulatedDevice1.LoRaDevice.DevAddr, existingCache);
            existingCache.TryAdd(loraDevice1.DevEUI, loraDevice1);
            existingCache.TryAdd(loraDevice2.DevEUI, loraDevice2);

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
       
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(loraDevice1, actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            loraDeviceFactoryMock.VerifyAll();
        }



        [Fact]
        public async Task When_Multiple_Devices_With_Same_DevAddr_Are_Returned_From_API_Should_Return_Matching_By_Mic()
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: serverConfiguration.GatewayID));
            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, this.loRaDeviceClient.Object);

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: serverConfiguration.GatewayID));
            simulatedDevice2.LoRaDevice.DeviceID = "00000002";
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice2, this.loRaDeviceClient.Object);

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            var devAddr = loraDevice1.DevAddr;

            // devices will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(new Twin());

            // Api service: search devices async
            var iotHubDeviceInfo1 = new IoTHubDeviceInfo(devAddr, loraDevice1.DevEUI, "");
            var iotHubDeviceInfo2 = new IoTHubDeviceInfo(devAddr, loraDevice2.DevEUI, "");
            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchDevicesAsync(serverConfiguration.GatewayID, devAddr, null, null, null))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo[]
                {
                    iotHubDeviceInfo2,
                    iotHubDeviceInfo1,
                }));

            // Device factory: create 2 devices
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo1)).Returns(loraDevice1);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo2)).Returns(loraDevice2);

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(loraDevice1, actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            loraDeviceFactoryMock.VerifyAll();

            // Both devices are in cache
            var cachedItem = this.cache.Get<LoRaDeviceRegistry.DevEUIDeviceDictionary>(devAddr);
            Assert.NotNull(cachedItem);
            Assert.Equal(2, cachedItem.Count); // 2 devices with same devAddr exist in cache

            // find device 1
            Assert.True(cachedItem.TryGetValue(loraDevice1.DevEUI, out var actualCachedLoRaDevice1));
            Assert.Same(loraDevice1, actualCachedLoRaDevice1);

            // find device 2
            Assert.True(cachedItem.TryGetValue(loraDevice2.DevEUI, out var actualCachedLoRaDevice2));
            Assert.Same(loraDevice2, actualCachedLoRaDevice2);
        }


        [Fact]
        public async Task When_New_ABP_Device_Instance_Is_Created_Should_Increment_FCntDown()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.serverConfiguration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(new Twin());


            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(actual, createdLoraDevice);

            // Device was searched by DevAddr
            apiService.Verify();

            // Device was created by factory
            loraDeviceFactoryMock.Verify();
        }
    }
}
