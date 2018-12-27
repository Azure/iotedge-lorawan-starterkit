using LoRaTools.LoRaMessage;
using LoRaWan.NetworkServer;
using LoRaWan.Test.Shared;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWanNetworkServer.Test
{
    public class LoRaDeviceRegistryTest
    {
        NetworkServerConfiguration configuration;
        private MemoryCache cache;
        private readonly Mock<ILoRaDeviceFactory> loraDeviceFactoryMock;

        public LoRaDeviceRegistryTest()
        {
            this.configuration = new NetworkServerConfiguration();
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.loraDeviceFactoryMock = new Mock<ILoRaDeviceFactory>();
        }


        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Not_In_Api_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchDevicesAsync(this.configuration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult());
            var target = new LoRaDeviceRegistry(this.configuration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.Verify();
        }


        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Should_Return_It()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            Assert.True(payload.CheckMic(simulatedDevice.LoRaDevice.NwkSKey)); // why is this failing?

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.configuration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));


            var createdLoraDevice = new TestLoRaDeviceAdapter(simulatedDevice);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);
               
            var target = new LoRaDeviceRegistry(this.configuration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(actual, createdLoraDevice);

            // Device was searched by DevAddr
            apiService.Verify();

            // Device was created by factory
            loraDeviceFactoryMock.Verify();
        }


        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Does_Not_Match_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            
            var cachedSimulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            cachedSimulatedDevice.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"; // make different than the payload
            cachedSimulatedDevice.LoRaDevice.DeviceID = "0000000000000002";

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "");
            apiService.Setup(x => x.SearchDevicesAsync(this.configuration.GatewayID, It.IsNotNull<string>(), null, null, null))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));


            var createdLoraDevice = new TestLoRaDeviceAdapter(cachedSimulatedDevice);
            loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);
               
            var target = new LoRaDeviceRegistry(this.configuration, this.cache, apiService.Object, loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);
            
            // Device was searched by DevAddr
            apiService.Verify();

            // Device was created by factory
            loraDeviceFactoryMock.Verify();
        }
    }
}
