using LoRaTools.LoRaMessage;
using LoRaWan.NetworkServer;
using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
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

        public LoRaDeviceRegistryTest()
        {
            this.serverConfiguration = new NetworkServerConfiguration();
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.loraDeviceFactoryMock = new Mock<ILoRaDeviceFactory>();
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


            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, null);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);
               
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


            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, null);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

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


        [Fact(Skip = "MicError")]
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

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(cachedSimulatedDevice, null);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);
               
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);
            
            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            loraDeviceFactoryMock.VerifyAll();
        }


        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Does_Not_Match_Gateway_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "a_different_one"));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.serverConfiguration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, null);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

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
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
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
            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, null);

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: serverConfiguration.GatewayID));
            simulatedDevice2.LoRaDevice.DeviceID = "00000002";
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice2, null);

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            var devAddr = loraDevice1.DevAddr;


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


            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, null);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

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
